using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SequencedDelivery
{
  public class Subscriber
  {
    string c = "";

    public Task Run()
    {
      return Task.WhenAll(
          ListenToTopicSubscriptionAsync(c, "sequenced_topic1", "sequenced_topic1_cyan"),
          ListenToTopicSubscriptionAsync(c, "sequenced_topic1", "sequenced_topic1_red"));
    }

    public async Task ListenToTopicSubscriptionAsync(string ConnectionString, string TopicName, string SubscriptionName)
    {
      var cts = new CancellationTokenSource();

      var allListeneres = Task.WhenAll(
          ReceiveMessagesFromTopicSubscriptionAsync(
              new SubscriptionClient(ConnectionString, TopicName, SubscriptionName), cts.Token)
      );

      await Task.WhenAll(
          Task.WhenAny(
              Task.Run(() => Console.ReadKey()),
              Task.Delay(TimeSpan.FromMinutes(5))
          ).ContinueWith((t) => cts.Cancel()),
          allListeneres);
    }

    private async Task ReceiveMessagesFromTopicSubscriptionAsync(SubscriptionClient client, CancellationToken token)
    {
      var doneReceiving = new TaskCompletionSource<bool>();

      token.Register(
          async () =>
          {
            await client.CloseAsync();
            doneReceiving.SetResult(true);
          });

      var sessionHandlerOptions = new SessionHandlerOptions(e => LogMessageExceptionHandler(e))
      {
        MessageWaitTimeout = TimeSpan.FromSeconds(5),
        MaxConcurrentSessions = 1,
        AutoComplete = false
      };

      client.RegisterSessionHandler(
          async (session, message, token1) =>
          {
            try
            {
              var stateData = await session.GetStateAsync();
              var sessionState = stateData != null ? Deserialize<SessionStateManager>(stateData) : new SessionStateManager();

              // check if message is next in the sequence
              if ((int)message.UserProperties["SequenceNumber"] == sessionState.LastProcessed + 1)
              {
                if (ProcessMessages(session.SessionId, message))
                {
                  await session.CompleteAsync(message.SystemProperties.LockToken);

                  sessionState.LastProcessed = ((int)message.UserProperties["SequenceNumber"]);
                  await session.SetStateAsync(Serialize<SessionStateManager>(sessionState));
                  if (message.UserProperties["IsLast"].ToString().ToLower() == "true")
                  {
                    // end of the session
                    await session.SetStateAsync(null);
                    await session.CloseAsync();
                  }
                }
                else
                {
                  await client.DeadLetterAsync(
                            message.SystemProperties.LockToken,
                            "Message is of the wrong type or could not be processed",
                            "Cannot deserialize this message as the type is unknown.");
                }
              }
              else
              {
                sessionState.DeferredList.Add(
                          (int)message.UserProperties["SequenceNumber"], message.SystemProperties.SequenceNumber);
                await session.DeferAsync(message.SystemProperties.LockToken);
                await session.SetStateAsync(Serialize(sessionState));
              }

              long lastProcessed = await ProcessNextMessagesWithSessionStateAsync(client, session, sessionState);
            }
            catch (Exception ex)
            {
              Console.WriteLine("ERROR: Unable to receive {0} from subscription: Exception {1}", message.MessageId, ex);
            }
          }, sessionHandlerOptions);

      await doneReceiving.Task;
    }

    private static async Task<long> ProcessNextMessagesWithSessionStateAsync(
        SubscriptionClient client, IMessageSession session, SessionStateManager sessionState)
    {
      int nextExpected = sessionState.LastProcessed + 1;
      long systemSequenceNumber;
      while (true)
      {
        if (!sessionState.DeferredList.TryGetValue(nextExpected, out systemSequenceNumber))
        {
          break;
        }

        var deferredMessage = await session.ReceiveDeferredMessageAsync(systemSequenceNumber);

        if (ProcessMessages(session.SessionId, deferredMessage))
        {
          await session.CompleteAsync(deferredMessage.SystemProperties.LockToken);

          if (deferredMessage.UserProperties["IsLast"].ToString().ToLower() == "true")
          {
            // end of the session
            await session.SetStateAsync(null);
            await session.CloseAsync();
          }
          else
          {
            sessionState.LastProcessed = ((int)deferredMessage.UserProperties["SequenceNumber"]);
            sessionState.DeferredList.Remove(nextExpected);
            await session.SetStateAsync(Serialize<SessionStateManager>(sessionState));
          }

        }
        else
        {
          await client.DeadLetterAsync(
              deferredMessage.SystemProperties.LockToken,
              "Message is of the wrong type or could not be processed",
              "Cannot deserialize this message as the type is unknown.");
          sessionState.DeferredList.Remove(nextExpected);
          await session.SetStateAsync(Serialize<SessionStateManager>(sessionState));
        }

        nextExpected++;
      }

      return systemSequenceNumber;
    }

    static object lockObj = new object();
    private static bool ProcessMessages(string sessionId, Message message)
    {

      var msg = Deserialize<dynamic>(message.Body);

      lock (lockObj)
      {
        var s = @$"
Message received: 
MessageId = {message.MessageId}
SequenceNumber = {message.SystemProperties.SequenceNumber}
EnqueuedTimeUtc = {message.SystemProperties.EnqueuedTimeUtc}
Session: {message.SessionId ?? sessionId}
Sequence: {((int)message.UserProperties["SequenceNumber"])}";

        ConsoleWrite(s, GetColor(message));
      }

      return true;
    }

    private static ConsoleColor GetColor(Message message)
    {
      string color = ((string)message.UserProperties["color"]).ToString().ToUpperInvariant();
      switch (color)
      {
        case "RED":
          return ConsoleColor.Red;
        case "CYAN":
          return ConsoleColor.Cyan;
        default:
          return ConsoleColor.White;
      }
    }

    public static byte[] Serialize<T>(T Item)
    {
      return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Item));
    }

    public static T Deserialize<T>(byte[] Item)
    {
      return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Item));
    }


    private Task LogMessageExceptionHandler(ExceptionReceivedEventArgs e)
    {
      ConsoleWrite($"Exception: \"{e.Exception.Message}\" {e.ExceptionReceivedContext.EntityPath}", ConsoleColor.Red);
      return Task.CompletedTask;
    }

    public static void ConsoleWrite(string Text, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      Console.WriteLine(Text, color);
      Console.ResetColor();
    }
  }
}

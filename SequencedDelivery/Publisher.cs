using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SequencedDelivery
{
  public class Publisher
  {
    bool isRelay = true;
    string c = "";

    public Task Run()
    {
      string dtString = DateTime.Now.ToString("yyyy-M-ddThh:mm:ss.ff");
      string sessionName = "sessionName";
      string transactionId = "transactionId";
      string instanceId = $"{dtString}";
      var sessionId = $"{sessionName}_{transactionId}_{instanceId}";

      List<dynamic> temp = new List<dynamic>();
      string color = "cyan";
      temp.Add(new { color = color, sequenceNumber = 6, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 5, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 9, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 4, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 10, isLast = true, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 7, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 2, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 1, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 3, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 8, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });

      color = "red";
      temp.Add(new { color = color, sequenceNumber = 6, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 5, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 9, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 4, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 10, isLast = true, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 7, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 2, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 1, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 3, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });
      temp.Add(new { color = color, sequenceNumber = 8, isLast = false, sessionName = sessionName, transactionId = transactionId, instanceId = instanceId, session = sessionId });

      string publishTo = isRelay ? "relay_topic1" : "sequenced_topic1";
      return PublishToOrderedSession(temp, c, publishTo);
    }


    private Task PublishToOrderedSession(List<dynamic> temp, string ConnectionString, string TopicName)
    {
      // creating Messages out of test data
      List<Message> messages = temp.Select(m =>
      {
        var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(m)))
        {
          ContentType = "application/json",
          MessageId = $"{m.sessionName}-{m.sequenceNumber}-{m.instanceId}-{Guid.NewGuid()}",
          Label = "Sequenced Test",
          TimeToLive = TimeSpan.FromMinutes(1),
          // SessionId = m.session,
        };

        // Message properties
        message.UserProperties.Add("SequenceNumber", m.sequenceNumber);
        message.UserProperties.Add("IsLast", m.isLast);

        message.UserProperties.Add("color", m.color);
        message.UserProperties.Add("SessionName", m.sessionName);
        message.UserProperties.Add("TransactionId", m.transactionId);
        message.UserProperties.Add("InstanceId", m.instanceId);

        return message;
      }).ToList();

      //send async
      var tasks = new List<Task>();
      var sender = new MessageSender(ConnectionString, TopicName);
      messages.ForEach(m => tasks.Add(sender.SendAsync(m)));

      return Task.WhenAll(tasks.ToArray());
    }
  }
}

using System;

namespace SequencedDelivery
{
  class Program
  {
    static void Main(string[] args)
    {
      try
      {
        Console.WriteLine("Publishing messages to Azure Service Bus");

        var publisher = new Publisher();
        publisher.Run().GetAwaiter().GetResult();

        Console.ReadKey();

        Console.WriteLine("Reading messages from Azure Service Bus");
        var subscriber = new Subscriber();
        subscriber.Run().GetAwaiter().GetResult();

        Console.ReadKey();
      }
      catch (Exception e)
      {
        Console.WriteLine(e.ToString());
      }
    }
  }
}

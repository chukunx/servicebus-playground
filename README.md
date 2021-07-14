ServiceBus Playground

# Sequenced Delivery

Azure Service Bus Queue/Topic does not ensure FIFO delivery. Out-of-order message may occur in various situations, for example:

- message isn't sent in expected order due to transient error or intermittent issue within application
- delayed delivery of messages due to unexpected incident [within Azure][1.3], or inconsistent state in Service Bus due to [failure on a single subsystem][1.4]
- various network issues

However, application side message sequencing is usually desired, the first step of which being consumer level sequence enforcement. In [Ordering Messages in Azure Service Bus][1.1], [Herald Gjura][1.2] outlined a solution that ensures this delivery behavior. Messages are delivered through sessions, within which out-of-order messages are deferred until all prior messages defined by particular sequence have been processed successfully.

## Things to consider

1. session over various entities: a common use case where a sequential execution is participated by messages from different entities
   1. `SessionId` : `{SessionName}_{TransactionId}_{InstanceId}`
      1. `SessionName`: name of the session where message should be executed in a predefined sequence
      2. `TransactionId`: uniquely identify a transaction
      3. `InstanceId`: uniquely identify an execution instance
   2. state maintaining solutions
      1. forward messages from original entities to a dedicated entity where session state can be maintained and enforced within ASB. For example: `SessionNameTopic/PartitionNameSubscription`
          1. `SessionId`: this may be constructed with ASB (action), Azure Functions, or a self-hosted application
          2. adding at least two more operations per message: forwarding, metadata action
      2. use centralized database to maintain session state.
         1. PrimaryKey/Id: `SessionId`
2. throughput baseline: a throughput baseline should be established
   1. { processingRate, exceptionRate } x { consumerReplica, ASB pressure level }
3. graceful start up and shutdown: resource to be initialized and disposed properly
   1. session closure: session completion, session exception, session expiration
   2. client closure: dispose consumer clients when shut down
4. scaling strategy for sequence entity consumers
   1. consumer can be scaled on demand

## Topological structure

```comment
(Publishers)
    |
  . |.  .   .   .   .   .   .   .   .   .   .   .   .
  . |-->    (Topic1)    --> (Sub1)      -->         .
  . |                   --> (Sub2_Fwd)  ----.       .
  . |                                       | SqlFilter 
  . |-->    (Topic2)    --> (Sub2_Fwd)  ----|   EXISTS(user.SessionName) AND EXISTS(user.TransactionId)
  .                                         |       .
  .     .-----------------------------------|       .
  .     |                                           .
  .     |               Action                      .
  .     |                   SET sys.SessionId = SessionName + TransactionId + InstanceId
  .     |-> (Topic3)    --> (SessionEnabled_Sub1)   --> (Subscriber)
  .                                                 .       |
  .  .  .   .   .   .   .   .   .   .   .   .   .   .       |
          Service Bus Instance                              |-----------------------..
                                                            |               |
                                                            Processor1      Processor2
```

## Subscriber Flowchart

```comment
(Subscriber)
                        [Message Received]
                            |
                        [Read session state]
                            |                       No
                        <Is the next in sequence>   --> [Update session state]  --> [Defer message]-.
                            |   Yes                                                                 |
                        [Process message]                                                           |
                            |       No                                                              |
                        <Success>   --> [DLQ]   --> ------------------------------------------------|
                            |   Yes                                                                 |
                        [Update session state]                                                      |
                            |                       Yes                                             |
                        <Is the last in sequence>   --> [Update session state]  --> [Close session]-|
                    No      |   No                                                                  |
[End processing]    <-- <If the next found deferred>    <-------------------------------------------|
    ^                       |   Yes                                                                 |
[Close session]         [Read and process the next]                                                 |
    |                       |       No                                                              |
[Update session state]  <Success>   --> [DLQ]   -->     [Update session state]  --------------------|
    |               Yes     |   Yes                 No          ^
    |-------------- <-- <Is the last in sequence>   --> --------|
```

[//]: # "open question"

[//]: # "references"
[1.1]: https://devblogs.microsoft.com/premier-developer/ordering-messages-in-azure-service-bus/ "Ordering Messages in Azure Service Bus"
[1.2]: https://www.linkedin.com/in/heraldgjura/ "Herald Gjura"
[1.3]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-async-messaging#issue-for-an-azure-dependency "Issue for an Azure dependency"
[1.4]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-async-messaging#service-bus-failure-on-a-single-subsystem "Service Bus failure on a single subsystem"

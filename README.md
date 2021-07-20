ServiceBus Playground

<!-- @import "[TOC]" {cmd="toc" depthFrom=1 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [1. Consumer Level Sequenced Message Delivery](#1-consumer-level-sequenced-message-delivery)
  - [1.1. Topological structure for message composition](#11-topological-structure-for-message-composition)
  - [1.2. Session Execution Flowchart](#12-session-execution-flowchart)
  - [1.3. Requirements/Considerations](#13-requirementsconsiderations)
  - [1.4. Improvements](#14-improvements)

<!-- /code_chunk_output -->

# 1. Consumer Level Sequenced Message Delivery

Azure Service Bus Queue/Topic does not ensure FIFO delivery. Out-of-order message may occur in various situations, for example:

- Issue with application: message isn't sent in expected order due to transient error or intermittent issue within application
- Issue with Azure: delayed delivery of messages due to unexpected incident [within Azure][1.3], or inconsistent state in Service Bus due to [failure on a single subsystem][1.4]
- Issue with network

However, application side message sequencing is usually desired, the first step of which being consumer level sequence enforcement. In [Ordering Messages in Azure Service Bus][1.1], [Herald Gjura][1.2] outlined a solution that ensures this delivery behavior. Messages are delivered through sessions, within which out-of-order messages are deferred until all prior messages defined by particular sequence have been processed successfully.

The three main parts of this solution:

- [Session][1.5] enabled composition subscription
- Sequential session execution using the [Message deferral][1.6] feature
- Sequence configurations

## 1.1. Topological structure for message composition

```comment
(Publishers) <SessionName, SequenceName, SequenceNumber>
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
  .     |-> (Topic3)    --> (SessionEnabled_Sub1)   --> (Subscriber(s))
  .                                                 .       |
  .  .  .   .   .   .   .   .   .   .   .   .   .   .       | <SessionName, SequenceName, ActivityInfo>
          Service Bus Instance                              |-----------------------..
                                                            |               |
                                                            Processor1      Processor2
```

1. Publishing stays mostly unchanged with minor addition
    - session configuration: <`SessionName`, `SequenceName`, `SequenceNumber`>
2. Existing subscriptions: no change, or can be disabled if desired
3. Forward subscriptions: forward messages target the original subscriptions to new session enabled entity
    - `SessionId` : `{SessionName}_{TransactionId}_{InstanceId}`
4. session enabled entity: receives message, make a copy, take actions, dispatch to destination subscription
5. Subscriber process messages and close session once completed
    - <`SessionName`, `SequenceName`, `ActivityInfo`>

## 1.2. Session Execution Flowchart

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

## 1.3. Requirements/Considerations

1. Session over various entities: a common use case where a sequential execution is participated by messages from different entities
   1. `SessionId` : `{SessionName}_{TransactionId}_{InstanceId}`
      1. `SessionName`: name of the session where message should be executed in a predefined sequence
      2. `TransactionId`: uniquely identifies a transaction
      3. `InstanceId`: uniquely identifies an execution instance
   2. state maintaining solutions
      1. [forward messages][1.3.1] from original entities to a dedicated entity where session state can be maintained and enforced within ASB. For example: `SessionNameTopic/PartitionNameSubscription`
          1. `SessionId`: this may be constructed with ASB (action), Azure Functions, or a self-hosted application
          2. adding at least two more operations per message: forwarding, metadata action
      2. use centralized database to maintain session state.
         1. PrimaryKeyValue/Id: `SessionId`
2. Scaling strategy and throughput baseline: a throughput baseline should be established
   1. { *consumerReplica*, `MaxConcurrentSessions`, *compositionRate* } x { *processingThroughput*, *exceptionRate*, *ASB pressure*, *consumer pressure* }
   2. *consumerReplica*: scale out -> consumer (down), ASB (probably up)
   3. `MaxConcurrentSessions`: scale out (probably also up) -> consumer (up), ASB (probably up)
      - process sessions in parallel: maintain sequence for each session while maximize throughput for each client
   4. **bottleneck** on the composite subscription if composition rate is high
      - limit it to a reasonable number
3. Graceful start up and shutdown: resource to be initialized and disposed properly
   1. session closure: session completion, session exception, session termination
   2. client closure: dispose consumer clients when shut down
4. Limitations
   1. maximum sessions: no official information (probably restricted by storage)
   2. maximum forwards: 4
   3. session state size: one message equivalent = 1MB (Premium)
5. Staled session
   1. never ending session sequence: incorrect configuration
   2. bad transaction: manual termination
   3. session error on closure: false positive - manual termination
6. DLQs/ErrorLogs
   1. source subscription (publisher failure)
      - when destination is disabled/reached quota: evaluate possibility, if happens manual intervention is required
   2. destination subscription (subscription failure)
      - message needs to be reprocessed
7. Cost
   1. extra operations for each session enabled message: forward (1) + action (1)
   2. extra space for session state

## 1.4. Improvements

1. Composition rate: this is due to the limitation of native forward behavior lacking of partitioning.
   - Use dedicated application/functions for finer control over forwarding, i.e. adding partitioning to forwarding logic so that more subscriptions can be used (dedicated topic, even ASB). These subscriptions are equivalent functionality-wise.
2. Manual intervention to handle staled sessions
   - Session termination: retrieve message from the session in question and manually close it.

[//]: # "open question"

[//]: # "references"
[1.1]: https://devblogs.microsoft.com/premier-developer/ordering-messages-in-azure-service-bus/ "Ordering Messages in Azure Service Bus"
[1.2]: https://www.linkedin.com/in/heraldgjura/ "Herald Gjura"
[1.3]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-async-messaging#issue-for-an-azure-dependency "Issue for an Azure dependency"
[1.4]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-async-messaging#service-bus-failure-on-a-single-subsystem "Service Bus failure on a single subsystem"
[1.5]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/message-sessions "Message sessions"
[1.6]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/message-deferral "Message deferral"
[1.3.1]: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-auto-forwarding "Chaining Service Bus entities with autoforwarding"

using ClipMate.Core.Events;
using ClipMate.Core.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for messaging infrastructure with real message passing between components.
/// Tests verify that IMessenger delivers messages correctly and components handle them properly.
/// These tests focus on message delivery mechanics without requiring full service integration.
/// NOTE: Uses isolated messenger instances to avoid test interference.
/// </summary>
[Category("Messaging")]
[Category("Integration")]
public class MessagingIntegrationTests : IntegrationTestBase
{
    /// <summary>
    /// Creates an isolated messenger for testing to avoid shared state issues.
    /// </summary>
    protected static IMessenger CreateMessenger() => new StrongReferenceMessenger();
    /// <summary>
    /// Tests encryption-related message delivery.
    /// Verifies that encryption coordination messages are properly delivered.
    /// </summary>
    public class EncryptionCoordinationTests : MessagingIntegrationTests
    {
        [Test]
        [Category("EncryptionCoordination")]
        public async Task EncryptionKeyExpired_MessageIsDelivered()
        {
            // Arrange: Real messenger with unique token
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var expiredEventReceived = false;

            // Subscribe to verify EncryptionKeyExpiredEvent is delivered
            messenger.Register<EncryptionKeyExpiredEvent>(token, (r, m) => expiredEventReceived = true);

            // Act: Send encryption key expired event
            messenger.Send(new EncryptionKeyExpiredEvent());

            // Assert: Event was received
            await Assert.That(expiredEventReceived).IsTrue();

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("EncryptionCoordination")]
        public async Task LockClipsRequest_MessageContainsCorrectData()
        {
            // Arrange
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var requestReceived = false;
            var receivedLockAll = false;
            IReadOnlyList<Guid>? receivedClipIds = null;

            messenger.Register<LockClipsRequestedEvent>(token, (r, m) =>
            {
                requestReceived = true;
                receivedLockAll = m.LockAll;
                receivedClipIds = m.ClipIds;
            });

            var expectedClipIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            // Act: Send LockClipsRequestedEvent with specific clip IDs
            messenger.Send(new LockClipsRequestedEvent(expectedClipIds, LockAll: false));

            // Assert: Message received with correct data
            await Assert.That(requestReceived).IsTrue();
            await Assert.That(receivedLockAll).IsFalse();
            await Assert.That(receivedClipIds).IsNotNull();
            await Assert.That(receivedClipIds!.Count).IsEqualTo(2);
            await Assert.That(receivedClipIds).IsEquivalentTo(expectedClipIds);

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("EncryptionCoordination")]
        public async Task EncryptClipsRequest_MessageDeliveredWithClipIds()
        {
            // Arrange: Use unique token to avoid cross-test interference
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var requestReceived = false;
            IReadOnlyList<Guid>? receivedClipIds = null;

            messenger.Register<EncryptClipsRequestedEvent>(token, (r, m) =>
            {
                requestReceived = true;
                receivedClipIds = m.ClipIds;
            });

            var clipIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            // Act: Send encrypt clips request
            messenger.Send(new EncryptClipsRequestedEvent(clipIds));

            // Assert: Request received with correct clip IDs
            await Assert.That(requestReceived).IsTrue();
            await Assert.That(receivedClipIds).IsNotNull();
            await Assert.That(receivedClipIds!.Count).IsEqualTo(3);
            await Assert.That(receivedClipIds).IsEquivalentTo(clipIds);

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("EncryptionCoordination")]
        public async Task ForgetEncryptionKeyRequest_MessageIsDelivered()
        {
            // Arrange
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var requestReceived = false;

            messenger.Register<ForgetEncryptionKeyRequestedEvent>(token, (r, m) => requestReceived = true);

            // Act: Send forget key request
            messenger.Send(new ForgetEncryptionKeyRequestedEvent());

            // Assert: Request was delivered
            await Assert.That(requestReceived).IsTrue();

            // Cleanup
            messenger.UnregisterAll(token);
        }
    }

    /// <summary>
    /// Tests multi-subscriber pattern where multiple components subscribe to the same message.
    /// Verifies that all subscribers receive and can handle the message independently.
    /// </summary>
    public class MultiSubscriberTests : MessagingIntegrationTests
    {
        [Test]
        [Category("MultiSubscriber")]
        public async Task StateRefreshRequest_DeliveredToAllSubscribers()
        {
            // Arrange: Multiple subscribers for same message
            var messenger = CreateMessenger();
            var subscriber1Received = false;
            var subscriber2Received = false;
            var subscriber3Received = false;

            var token1 = Guid.NewGuid();
            messenger.Register<StateRefreshRequestedEvent>(token1, (r, m) => subscriber1Received = true);

            var token2 = Guid.NewGuid();
            messenger.Register<StateRefreshRequestedEvent>(token2, (r, m) => subscriber2Received = true);

            var token3 = Guid.NewGuid();
            messenger.Register<StateRefreshRequestedEvent>(token3, (r, m) => subscriber3Received = true);

            // Act: Send message
            messenger.Send(new StateRefreshRequestedEvent());

            // Assert: All subscribers received the message
            await Assert.That(subscriber1Received).IsTrue();
            await Assert.That(subscriber2Received).IsTrue();
            await Assert.That(subscriber3Received).IsTrue();

            // Cleanup
            messenger.UnregisterAll(token1);
            messenger.UnregisterAll(token2);
            messenger.UnregisterAll(token3);
        }

        [Test]
        [Category("MultiSubscriber")]
        public async Task ClipSelectedEvent_DeliveredWithCorrectData()
        {
            // Arrange
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            Clip? receivedClip = null;
            string? receivedDatabaseKey = null;

            messenger.Register<ClipSelectedEvent>(token, (r, m) =>
            {
                receivedClip = m.SelectedClip;
                receivedDatabaseKey = m.DatabaseKey;
            });

            const string expectedDatabaseKey = "TestDB";

            // Act: Send ClipSelectedEvent with null clip
            messenger.Send(new ClipSelectedEvent(null, expectedDatabaseKey));

            // Assert: Subscriber received correct data
            await Assert.That(receivedClip).IsNull();
            await Assert.That(receivedDatabaseKey).IsEqualTo(expectedDatabaseKey);

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("MultiSubscriber")]
        public async Task ClipCacheExpiredMessage_DeliveredToMultipleComponents()
        {
            // Arrange: Simulate ClipListViewModel and ClipViewerWindowManager both subscribed
            var messenger = CreateMessenger();
            var clipListReceived = false;
            var clipViewerReceived = false;
            var receivedClipId = Guid.Empty;

            var token1 = Guid.NewGuid();
            // Simulate ClipListViewModel subscription
            messenger.Register<ClipCacheExpiredMessage>(token1, (r, m) =>
            {
                clipListReceived = true;
                receivedClipId = m.ClipId;
            });

            // Simulate ClipViewerWindowManager subscription
            var token2 = Guid.NewGuid();
            messenger.Register<ClipCacheExpiredMessage>(token2, (r, m) =>
            {
                clipViewerReceived = true;
            });

            var expiredClipId = Guid.NewGuid();

            // Act: Send cache expired message
            messenger.Send(new ClipCacheExpiredMessage(expiredClipId));

            // Assert: Both components received the message
            await Assert.That(clipListReceived).IsTrue();
            await Assert.That(clipViewerReceived).IsTrue();
            await Assert.That(receivedClipId).IsEqualTo(expiredClipId);

            // Cleanup
            messenger.UnregisterAll(token1);
            messenger.UnregisterAll(token2);
        }
    }

    /// <summary>
    /// Tests message delivery timing and ordering to ensure messages are processed correctly.
    /// NOTE: These tests use WeakReferenceMessenger.Default and must run serially to avoid interference.
    /// </summary>
    public class MessageOrderingTests : MessagingIntegrationTests
    {
        [Test]
        [Category("MessageOrdering")]
        [NotInParallel]
        public async Task MultipleMessages_DeliveredInSendOrder()
        {
            // Arrange
            var messenger = CreateMessenger();
            var receivedMessages = new List<string>();

            var token1 = Guid.NewGuid();
            messenger.Register<StateRefreshRequestedEvent>(token1, (r, m) =>
                receivedMessages.Add("StateRefresh"));

            var token2 = Guid.NewGuid();
            messenger.Register<ClipCacheExpiredMessage>(token2, (r, m) =>
                receivedMessages.Add($"CacheExpired:{m.ClipId}"));

            var clipId1 = Guid.NewGuid();
            var clipId2 = Guid.NewGuid();

            // Act: Send messages in sequence
            messenger.Send(new StateRefreshRequestedEvent());
            messenger.Send(new ClipCacheExpiredMessage(clipId1));
            messenger.Send(new StateRefreshRequestedEvent());
            messenger.Send(new ClipCacheExpiredMessage(clipId2));

            // Assert: Messages received in correct order
            await Assert.That(receivedMessages.Count).IsEqualTo(4);
            await Assert.That(receivedMessages[0]).IsEqualTo("StateRefresh");
            await Assert.That(receivedMessages[1]).IsEqualTo($"CacheExpired:{clipId1}");
            await Assert.That(receivedMessages[2]).IsEqualTo("StateRefresh");
            await Assert.That(receivedMessages[3]).IsEqualTo($"CacheExpired:{clipId2}");

            // Cleanup
            messenger.UnregisterAll(token1);
            messenger.UnregisterAll(token2);
        }

        [Test]
        [Category("MessageOrdering")]
        public async Task CascadingMessages_DeliveredSequentially()
        {
            // Arrange: Test message cascades (one message triggering another)
            var messenger = CreateMessenger();
            var receivedMessages = new List<string>();

            var token1 = Guid.NewGuid();
            // First subscriber listens to StateRefresh and sends EncryptionKeyExpiredEvent
            messenger.Register<StateRefreshRequestedEvent>(token1, (r, m) =>
            {
                receivedMessages.Add("StateRefresh");
                messenger.Send(new EncryptionKeyExpiredEvent());
            });

            var token2 = Guid.NewGuid();
            messenger.Register<EncryptionKeyExpiredEvent>(token2, (r, m) =>
                receivedMessages.Add("KeyExpired"));

            // Act: Send initial message that triggers cascade
            messenger.Send(new StateRefreshRequestedEvent());

            // Assert: Both messages received in cascade order
            await Assert.That(receivedMessages.Count).IsEqualTo(2);
            await Assert.That(receivedMessages[0]).IsEqualTo("StateRefresh");
            await Assert.That(receivedMessages[1]).IsEqualTo("KeyExpired");

            // Cleanup
            messenger.UnregisterAll(token1);
            messenger.UnregisterAll(token2);
        }
    }

    /// <summary>
    /// Tests unregistration and cleanup to ensure subscribers are properly removed.
    /// NOTE: These tests use WeakReferenceMessenger.Default and must run serially to avoid interference.
    /// </summary>
    public class SubscriberLifecycleTests : MessagingIntegrationTests
    {
        [Test]
        [Category("SubscriberLifecycle")]
        public async Task UnregisterAll_StopsMessageDelivery()
        {
            // Arrange
            var messenger = CreateMessenger();
            var recipient = new object(); // Need a recipient object for registration
            var receivedCount = 0;

            messenger.Register<StateRefreshRequestedEvent>(recipient, (r, m) => receivedCount++);

            // Act: Send message, verify receipt
            messenger.Send(new StateRefreshRequestedEvent());
            await Task.Delay(10); // Allow message processing
            await Assert.That(receivedCount).IsEqualTo(1);

            // Unregister
            messenger.UnregisterAll(recipient);

            // Send again
            messenger.Send(new StateRefreshRequestedEvent());
            await Task.Delay(10); // Allow message processing

            // Assert: Count didn't change after unregister (still 1, not 2)
            await Assert.That(receivedCount).IsEqualTo(1);
        }

        [Test]
        [Category("SubscriberLifecycle")]
        public async Task Unregister_SpecificMessage_StopsOnlyThatMessage()
        {
            // Arrange
            var messenger = CreateMessenger();
            var recipient = new object();
            var stateRefreshCount = 0;
            var clipSelectedCount = 0;

            messenger.Register<StateRefreshRequestedEvent>(recipient, (r, m) => stateRefreshCount++);
            messenger.Register<ClipSelectedEvent>(recipient, (r, m) => clipSelectedCount++);

            // Act: Send both messages
            messenger.Send(new StateRefreshRequestedEvent());
            messenger.Send(new ClipSelectedEvent(null));
            await Task.Delay(10); // Allow message processing

            await Assert.That(stateRefreshCount).IsEqualTo(1);
            await Assert.That(clipSelectedCount).IsEqualTo(1);

            // Unregister only StateRefreshRequestedEvent
            messenger.Unregister<StateRefreshRequestedEvent>(recipient);

            // Send both messages again
            messenger.Send(new StateRefreshRequestedEvent());
            messenger.Send(new ClipSelectedEvent(null));
            await Task.Delay(10); // Allow message processing

            // Assert: StateRefresh count unchanged (1), ClipSelected incremented (2)
            await Assert.That(stateRefreshCount).IsEqualTo(1);
            await Assert.That(clipSelectedCount).IsEqualTo(2);

            // Cleanup
            messenger.UnregisterAll(recipient);
        }

        [Test]
        [Category("SubscriberLifecycle")]
        public async Task MultipleTokens_CanBeUnregisteredIndependently()
        {
            // Arrange
            var messenger = CreateMessenger();
            var recipient1 = new object();
            var recipient2 = new object();
            var recipient1Count = 0;
            var recipient2Count = 0;

            messenger.Register<StateRefreshRequestedEvent>(recipient1, (r, m) => recipient1Count++);
            messenger.Register<StateRefreshRequestedEvent>(recipient2, (r, m) => recipient2Count++);

            // Act: Send message
            messenger.Send(new StateRefreshRequestedEvent());
            await Task.Delay(10); // Allow message processing

            await Assert.That(recipient1Count).IsEqualTo(1);
            await Assert.That(recipient2Count).IsEqualTo(1);

            // Unregister recipient1
            messenger.UnregisterAll(recipient1);

            // Send again
            messenger.Send(new StateRefreshRequestedEvent());
            await Task.Delay(10); // Allow message processing

            // Assert: recipient1 count unchanged (1), recipient2 incremented (2)
            await Assert.That(recipient1Count).IsEqualTo(1);
            await Assert.That(recipient2Count).IsEqualTo(2);

            // Cleanup
            messenger.UnregisterAll(recipient2);
        }

        [Test]
        [Category("SubscriberLifecycle")]
        public async Task Registration_WithUniqueToken_IsolatesSubscriber()
        {
            // Arrange: Demonstrate that unique tokens provide isolation
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var receivedCount = 0;

            messenger.Register<ForgetEncryptionKeyRequestedEvent>(token, (r, m) => receivedCount++);

            // Act: Send message
            messenger.Send(new ForgetEncryptionKeyRequestedEvent());

            // Assert: Message received
            await Assert.That(receivedCount).IsEqualTo(1);

            // Cleanup
            messenger.UnregisterAll(token);
        }
    }

    /// <summary>
    /// Tests request-response patterns using messages for operations.
    /// </summary>
    public class RequestResponseTests : MessagingIntegrationTests
    {
        [Test]
        [Category("RequestResponse")]
        public async Task EncryptClipsRequest_WithEmptyClipIds_DeliveredCorrectly()
        {
            // Arrange: Test that requests with no clip IDs are delivered as expected
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var requestReceived = false;
            IReadOnlyList<Guid>? receivedClipIds = null;

            messenger.Register<EncryptClipsRequestedEvent>(token, (r, m) =>
            {
                requestReceived = true;
                receivedClipIds = m.ClipIds;
            });

            // Act: Send request with empty clip list
            messenger.Send(new EncryptClipsRequestedEvent(Array.Empty<Guid>()));

            // Assert: Request was delivered with empty list
            await Assert.That(requestReceived).IsTrue();
            await Assert.That(receivedClipIds).IsNotNull();
            await Assert.That(receivedClipIds!.Count).IsEqualTo(0);

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("RequestResponse")]
        public async Task LockClipsRequest_WithLockAllFlag_DeliveredCorrectly()
        {
            // Arrange
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var lockAllReceived = false;
            IReadOnlyList<Guid>? receivedClipIds = null;

            messenger.Register<LockClipsRequestedEvent>(token, (r, m) =>
            {
                lockAllReceived = m.LockAll;
                receivedClipIds = m.ClipIds;
            });

            // Act: Send LockAll request
            messenger.Send(new LockClipsRequestedEvent(Array.Empty<Guid>(), LockAll: true));

            // Assert: LockAll flag and empty ClipIds received correctly
            await Assert.That(lockAllReceived).IsTrue();
            await Assert.That(receivedClipIds).IsNotNull();
            await Assert.That(receivedClipIds!.Count).IsEqualTo(0);

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("RequestResponse")]
        public async Task DecryptClipsRequest_DeliveredWithClipIds()
        {
            // Arrange
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var requestReceived = false;
            IReadOnlyList<Guid>? receivedClipIds = null;

            messenger.Register<DecryptClipsRequestedEvent>(token, (r, m) =>
            {
                requestReceived = true;
                receivedClipIds = m.ClipIds;
            });

            var clipIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            // Act: Send decrypt request
            messenger.Send(new DecryptClipsRequestedEvent(clipIds));

            // Assert: Request delivered correctly
            await Assert.That(requestReceived).IsTrue();
            await Assert.That(receivedClipIds).IsNotNull();
            await Assert.That(receivedClipIds!.Count).IsEqualTo(2);
            await Assert.That(receivedClipIds).IsEquivalentTo(clipIds);

            // Cleanup
            messenger.UnregisterAll(token);
        }

        [Test]
        [Category("RequestResponse")]
        public async Task DeleteClipsRequest_DeliveredWithClipIds()
        {
            // Arrange
            var messenger = CreateMessenger();
            var token = Guid.NewGuid();
            var requestReceived = false;
            IReadOnlyList<Guid>? receivedClipIds = null;

            messenger.Register<DeleteClipsRequestedEvent>(token, (r, m) =>
            {
                requestReceived = true;
                receivedClipIds = m.ClipIds;
            });

            var clipIds = new List<Guid> { Guid.NewGuid() };

            // Act: Send delete request
            messenger.Send(new DeleteClipsRequestedEvent(clipIds));

            // Assert: Request delivered correctly
            await Assert.That(requestReceived).IsTrue();
            await Assert.That(receivedClipIds).IsNotNull();
            await Assert.That(receivedClipIds!.Count).IsEqualTo(1);
            await Assert.That(receivedClipIds).IsEquivalentTo(clipIds);

            // Cleanup
            messenger.UnregisterAll(token);
        }
    }
}

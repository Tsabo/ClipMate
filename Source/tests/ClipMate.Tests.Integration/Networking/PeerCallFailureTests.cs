using System.Security.Authentication;
using ClipMate.Networking.SharedDatabases;
using Grpc.Core;

namespace ClipMate.Tests.Integration.Networking;

public class PeerCallFailureTests
{
    [Test]
    public async Task IsNoReply_InternalFromAnAbortedHandshake_IsTrue()
    {
        // Arrange: how Grpc.Net.Client reports a TLS handshake the Peer aborted (no gRPC status ever arrived).
        var exception = new RpcException(new Status(StatusCode.Internal, "Error starting gRPC call.",
            new HttpRequestException("The SSL connection could not be established.", new AuthenticationException("Handshake failed."))));

        // Act / Assert
        await Assert.That(PeerCallFailure.IsNoReply(exception)).IsTrue();
    }

    [Test]
    [Arguments(StatusCode.Unavailable)]
    [Arguments(StatusCode.DeadlineExceeded)]
    public async Task IsNoReply_PeerNotReachedOrTooSlow_IsTrue(StatusCode status)
    {
        // Act / Assert
        await Assert.That(PeerCallFailure.IsNoReply(new RpcException(new Status(status, "x")))).IsTrue();
    }

    [Test]
    [Arguments(StatusCode.Internal)]
    [Arguments(StatusCode.Unknown)]
    [Arguments(StatusCode.ResourceExhausted)]
    [Arguments(StatusCode.InvalidArgument)]
    public async Task IsNoReply_StatusSentByThePeer_IsFalse(StatusCode status)
    {
        // Act / Assert
        await Assert.That(PeerCallFailure.IsNoReply(new RpcException(new Status(status, "Sent by the Peer.")))).IsFalse();
    }
}

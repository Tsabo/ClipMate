using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

public class PairingDialogViewModelTests
{
    private static readonly DeviceId _laptop = DeviceId.FromCertificateBytes([1]);
    private static readonly DeviceId _desktop = DeviceId.FromCertificateBytes([2]);

    [Test]
    public async Task ForApproval_ShowsTheVerificationCodeAndPrefillsShowAsWithTheDeviceName()
    {
        // Act
        var viewModel = PairingDialogViewModel.ForApproval(new PairingRequest(_laptop, "LAPTOP", "K7QM-2XPA"));

        // Assert
        await Assert.That(viewModel.VerificationCode).IsEqualTo("K7QM-2XPA");
        await Assert.That(viewModel.ShowAs).IsEqualTo("LAPTOP");
        await Assert.That(viewModel.OtherDeviceName).IsEqualTo("LAPTOP");
        await Assert.That(viewModel.OtherDeviceId).IsEqualTo(_laptop.ToDisplayString());
    }

    [Test]
    public async Task ForApproval_Accept_DecidesWithTheAliasAndCloses()
    {
        // Arrange
        var viewModel = PairingDialogViewModel.ForApproval(new PairingRequest(_laptop, "LAPTOP", "K7QM-2XPA"));
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;
        viewModel.ShowAs = "  Travel laptop ";

        // Act
        await viewModel.AcceptCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(await viewModel.Decision).IsEqualTo(PairingDecision.Accept("Travel laptop"));
        await Assert.That(closed).IsTrue();
    }

    [Test]
    public async Task ForApproval_AcceptWithShowAsUnchanged_SetsNoAlias()
    {
        // Arrange
        var viewModel = PairingDialogViewModel.ForApproval(new PairingRequest(_laptop, "LAPTOP", "K7QM-2XPA"));

        // Act
        await viewModel.AcceptCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(await viewModel.Decision).IsEqualTo(PairingDecision.Accept(null));
    }

    [Test]
    public async Task ForApproval_Decline_DecidesDeclinedAndCloses()
    {
        // Arrange
        var viewModel = PairingDialogViewModel.ForApproval(new PairingRequest(_laptop, "LAPTOP", "K7QM-2XPA"));
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        // Act
        viewModel.DeclineCommand.Execute(null);

        // Assert
        await Assert.That(await viewModel.Decision).IsEqualTo(PairingDecision.Declined);
        await Assert.That(closed).IsTrue();
    }

    [Test]
    public async Task ForRequest_Accept_CompletesPairingWithTheAlias()
    {
        // Arrange
        var session = CreateSession(PairingStatus.Accepted);
        A.CallTo(() => session.AcceptAsync(A<string?>._, A<CancellationToken>._)).Returns(PairingStatus.Paired);
        var viewModel = PairingDialogViewModel.ForRequest(session);
        viewModel.ShowAs = "Office desktop";

        // Act
        await viewModel.AcceptCommand.ExecuteAsync(null);

        // Assert
        A.CallTo(() => session.AcceptAsync("Office desktop", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await Assert.That(viewModel.Status).IsEqualTo(PairingStatus.Paired);
        await Assert.That(viewModel.StatusText).IsEqualTo("Paired with DESKTOP.");
        await Assert.That(viewModel.IsFinished).IsTrue();
    }

    [Test]
    public async Task ForRequest_ShowsTheSessionsVerificationCode()
    {
        // Act
        var viewModel = PairingDialogViewModel.ForRequest(CreateSession(PairingStatus.Accepted));

        // Assert
        await Assert.That(viewModel.VerificationCode).IsEqualTo("ABCD-EFGH");
        await Assert.That(viewModel.ShowAs).IsEqualTo("DESKTOP");
    }

    [Test]
    public async Task ForRequest_WhenTheOtherDeviceDeclines_SaysSoAndCannotAccept()
    {
        // Arrange
        var session = CreateSession(PairingStatus.Declined);

        // Act
        var viewModel = PairingDialogViewModel.ForRequest(session);
        await viewModel.WhenPeerResponded;

        // Assert
        await Assert.That(viewModel.StatusText).IsEqualTo("DESKTOP declined the pairing request.");
        await Assert.That(viewModel.AcceptCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.IsFinished).IsTrue();
    }

    [Test]
    public async Task ForRequest_Decline_CancelsTheRequest()
    {
        // Arrange
        var session = CreateSession(PairingStatus.Accepted);
        var viewModel = PairingDialogViewModel.ForRequest(session);

        // Act
        viewModel.DeclineCommand.Execute(null);

        // Assert
        A.CallTo(() => session.Cancel()).MustHaveHappenedOnceExactly();
    }

    private static IPairingSession CreateSession(PairingStatus peerResponse)
    {
        var session = A.Fake<IPairingSession>();
        A.CallTo(() => session.Target).Returns(new PairingTarget(_desktop, "DESKTOP", "192.168.1.20:47823"));
        A.CallTo(() => session.VerificationCode).Returns("ABCD-EFGH");
        A.CallTo(() => session.PeerResponse).Returns(Task.FromResult(peerResponse));
        return session;
    }
}

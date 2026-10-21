using System.Text.RegularExpressions;
using ClipMate.Core.Models.Networking;

namespace ClipMate.Tests.Unit.Models.Networking;

public partial class VerificationCodeTests
{
    private static readonly DeviceId _first = DeviceId.FromCertificateBytes([1]);
    private static readonly DeviceId _second = DeviceId.FromCertificateBytes([2]);
    private static readonly DeviceId _third = DeviceId.FromCertificateBytes([3]);

    [Test]
    public async Task Derive_IsTheSameOnBothDevices()
    {
        // Act
        var requesterCode = VerificationCode.Derive(_first, _second);
        var approverCode = VerificationCode.Derive(_second, _first);

        // Assert
        await Assert.That(requesterCode).IsEqualTo(approverCode);
    }

    [Test]
    public async Task Derive_IsTwoGroupsOfFourBase32Characters()
    {
        // Act
        var code = VerificationCode.Derive(_first, _second);

        // Assert
        await Assert.That(CodeFormat().IsMatch(code)).IsTrue();
    }

    [Test]
    public async Task Derive_DiffersWhenEitherDeviceDiffers()
    {
        // Act
        var code = VerificationCode.Derive(_first, _second);
        var otherRequester = VerificationCode.Derive(_third, _second);
        var otherApprover = VerificationCode.Derive(_first, _third);

        // Assert
        await Assert.That(otherRequester).IsNotEqualTo(code);
        await Assert.That(otherApprover).IsNotEqualTo(code);
    }

    [GeneratedRegex("^[A-Z2-7]{4}-[A-Z2-7]{4}$")]
    private static partial Regex CodeFormat();
}

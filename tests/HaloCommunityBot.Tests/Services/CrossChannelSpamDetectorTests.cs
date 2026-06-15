using DiscordBot.Services;
using Xunit;

namespace HaloCommunityBot.Tests.Services;

public class CrossChannelSpamDetectorTests
{
    [Fact]
    public void ComputeFingerprint_TextOnly_ReturnsTextWithEmptyAttachments()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("hello world", []);
        Assert.Equal("hello world|", fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_AttachmentsOnly_ReturnsSignatureBasedFingerprint()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("", [
            new AttachmentInfo("zebra.png", 200, 640, 480, "image/png", false),
            new AttachmentInfo("apple.jpg", 100, 320, 200, "image/jpeg", false)
        ]);

        Assert.Equal("|image/jpeg:100:320:200:.jpg:0,image/png:200:640:480:.png:0", fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_TextAndAttachments_ReturnsBothParts()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("check this", [
            new AttachmentInfo("b.png", 200, 640, 480, "image/png", false),
            new AttachmentInfo("a.png", 100, 320, 200, "image/png", false)
        ]);

        Assert.Equal("check this|image/png:100:320:200:.png:0,image/png:200:640:480:.png:0", fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_EmptyContentNoAttachments_ReturnsEmpty()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("", []);
        Assert.Equal(string.Empty, fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_AttachmentSignatureOrderIsAlwaysSorted()
    {
        var fp1 = CrossChannelSpamDetector.ComputeFingerprint("", [
            new AttachmentInfo("z.png", 300, 100, 100, "image/png", false),
            new AttachmentInfo("a.png", 100, 100, 100, "image/png", false),
            new AttachmentInfo("m.png", 200, 100, 100, "image/png", false)
        ]);
        var fp2 = CrossChannelSpamDetector.ComputeFingerprint("", [
            new AttachmentInfo("a.png", 100, 100, 100, "image/png", false),
            new AttachmentInfo("m.png", 200, 100, 100, "image/png", false),
            new AttachmentInfo("z.png", 300, 100, 100, "image/png", false)
        ]);

        Assert.Equal(fp1, fp2);
    }
}

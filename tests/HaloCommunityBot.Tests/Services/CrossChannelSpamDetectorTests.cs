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
    public void ComputeFingerprint_AttachmentsOnly_ReturnsEmptyTextWithSortedFilenames()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("", ["zebra.png", "apple.jpg"]);
        Assert.Equal("|apple.jpg,zebra.png", fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_TextAndAttachments_ReturnsBothParts()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("check this", ["b.png", "a.png"]);
        Assert.Equal("check this|a.png,b.png", fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_EmptyContentNoAttachments_ReturnsEmpty()
    {
        var fingerprint = CrossChannelSpamDetector.ComputeFingerprint("", []);
        Assert.Equal(string.Empty, fingerprint);
    }

    [Fact]
    public void ComputeFingerprint_AttachmentOrderIsAlwaysSorted()
    {
        var fp1 = CrossChannelSpamDetector.ComputeFingerprint("", ["z.png", "a.png", "m.png"]);
        var fp2 = CrossChannelSpamDetector.ComputeFingerprint("", ["a.png", "m.png", "z.png"]);
        Assert.Equal(fp1, fp2);
    }
}

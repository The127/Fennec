using Fennec.App.Messages;
using Fennec.App.Services.ScreenCapture;

namespace Fennec.App.Tests.Services;

public class ScreenShareCursorTests
{
    // --- Cursor name mapping ---

    [Theory]
    [InlineData("left_ptr", CursorType.Arrow)]
    [InlineData("default", CursorType.Arrow)]
    [InlineData("arrow", CursorType.Arrow)]
    [InlineData("hand2", CursorType.Hand)]
    [InlineData("pointer", CursorType.Hand)]
    [InlineData("pointing_hand", CursorType.Hand)]
    [InlineData("xterm", CursorType.Text)]
    [InlineData("text", CursorType.Text)]
    [InlineData("ibeam", CursorType.Text)]
    [InlineData("crosshair", CursorType.Crosshair)]
    [InlineData("cross", CursorType.Crosshair)]
    [InlineData("sb_v_double_arrow", CursorType.ResizeNS)]
    [InlineData("ns-resize", CursorType.ResizeNS)]
    [InlineData("size_ver", CursorType.ResizeNS)]
    [InlineData("top_side", CursorType.ResizeNS)]
    [InlineData("bottom_side", CursorType.ResizeNS)]
    [InlineData("sb_h_double_arrow", CursorType.ResizeEW)]
    [InlineData("ew-resize", CursorType.ResizeEW)]
    [InlineData("size_hor", CursorType.ResizeEW)]
    [InlineData("left_side", CursorType.ResizeEW)]
    [InlineData("right_side", CursorType.ResizeEW)]
    [InlineData("size_bdiag", CursorType.ResizeNESW)]
    [InlineData("nesw-resize", CursorType.ResizeNESW)]
    [InlineData("bottom_left_corner", CursorType.ResizeNESW)]
    [InlineData("top_right_corner", CursorType.ResizeNESW)]
    [InlineData("size_fdiag", CursorType.ResizeNWSE)]
    [InlineData("nwse-resize", CursorType.ResizeNWSE)]
    [InlineData("bottom_right_corner", CursorType.ResizeNWSE)]
    [InlineData("top_left_corner", CursorType.ResizeNWSE)]
    [InlineData("fleur", CursorType.Move)]
    [InlineData("move", CursorType.Move)]
    [InlineData("grab", CursorType.Move)]
    [InlineData("grabbing", CursorType.Move)]
    [InlineData("all-scroll", CursorType.Move)]
    [InlineData("not-allowed", CursorType.NotAllowed)]
    [InlineData("crossed_circle", CursorType.NotAllowed)]
    [InlineData("X_cursor", CursorType.NotAllowed)]
    [InlineData("watch", CursorType.Wait)]
    [InlineData("wait", CursorType.Wait)]
    [InlineData("left_ptr_watch", CursorType.Wait)]
    [InlineData("question_arrow", CursorType.Help)]
    [InlineData("help", CursorType.Help)]
    [InlineData("whats_this", CursorType.Help)]
    public void MapCursorName_KnownNames_ReturnsCorrectType(string name, CursorType expected)
    {
        Assert.Equal(expected, LinuxCursorPositionService.MapCursorName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("some_unknown_cursor")]
    [InlineData("custom_theme_cursor_123")]
    public void MapCursorName_UnknownNames_FallsBackToArrow(string name)
    {
        Assert.Equal(CursorType.Arrow, LinuxCursorPositionService.MapCursorName(name));
    }

    [Fact]
    public void MapCursorName_IsCaseInsensitive()
    {
        Assert.Equal(CursorType.Hand, LinuxCursorPositionService.MapCursorName("POINTER"));
        Assert.Equal(CursorType.Text, LinuxCursorPositionService.MapCursorName("Xterm"));
        Assert.Equal(CursorType.Wait, LinuxCursorPositionService.MapCursorName("WATCH"));
    }

    // --- 10-byte data channel serialization ---

    [Theory]
    [InlineData(0.5f, 0.75f, CursorType.Arrow, true)]
    [InlineData(0.0f, 1.0f, CursorType.Hand, false)]
    [InlineData(0.123f, 0.456f, CursorType.ResizeNWSE, true)]
    [InlineData(1.0f, 0.0f, CursorType.NotAllowed, false)]
    public void Serialization_Roundtrip_PreservesAllFields(float x, float y, CursorType type, bool isVisible)
    {
        // Serialize (same logic as VoiceCallService.OnCursorChanged)
        var data = new byte[10];
        BitConverter.TryWriteBytes(data.AsSpan(0, 4), x);
        BitConverter.TryWriteBytes(data.AsSpan(4, 4), y);
        data[8] = (byte)type;
        data[9] = isVisible ? (byte)1 : (byte)0;

        // Deserialize (same logic as ondatachannel handler)
        var deserializedX = BitConverter.ToSingle(data, 0);
        var deserializedY = BitConverter.ToSingle(data, 4);
        var deserializedType = (CursorType)data[8];
        var deserializedVisible = data[9] != 0;

        Assert.Equal(x, deserializedX);
        Assert.Equal(y, deserializedY);
        Assert.Equal(type, deserializedType);
        Assert.Equal(isVisible, deserializedVisible);
    }

    [Fact]
    public void Deserialization_9ByteLegacy_DefaultsVisibleToTrue()
    {
        // Old 9-byte format from a sender that hasn't been updated
        var data = new byte[9];
        BitConverter.TryWriteBytes(data.AsSpan(0, 4), 0.5f);
        BitConverter.TryWriteBytes(data.AsSpan(4, 4), 0.25f);
        data[8] = (byte)CursorType.Text;

        // Receiver logic (backwards compatible)
        Assert.True(data.Length >= 9);
        var x = BitConverter.ToSingle(data, 0);
        var y = BitConverter.ToSingle(data, 4);
        var cursorType = (CursorType)data[8];
        var isVisible = data.Length >= 10 ? data[9] != 0 : true;

        Assert.Equal(0.5f, x);
        Assert.Equal(0.25f, y);
        Assert.Equal(CursorType.Text, cursorType);
        Assert.True(isVisible);
    }

    [Fact]
    public void Serialization_AllCursorTypes_FitInSingleByte()
    {
        foreach (CursorType type in Enum.GetValues<CursorType>())
        {
            var b = (byte)type;
            var roundtripped = (CursorType)b;
            Assert.Equal(type, roundtripped);
        }
    }
}

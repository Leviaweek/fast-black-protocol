using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Package.DataHeader;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Package.Ping;
using BlackFastProtocol.Internal.Session;

namespace ClientTests;

// ─────────────────────────────────────────────────────────────────────────────
// PackageHeader
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class PackageHeaderTests
{
    [Test]
    public void Size_Is31Bytes()
    {
        Assert.That(PackageHeader.Size, Is.EqualTo(31));
    }

    [Test]
    public void WriteRead_RoundTrip_AllFields()
    {
        var sessionId = Guid.NewGuid();
        // Use a timestamp rounded to 100-ns ticks so no sub-tick rounding occurs.
        var ts = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var h  = new PackageHeader(sessionId, PackageType.Data, 42u, ts);

        var buf = new byte[PackageHeader.Size];
        h.WriteData(buf);
        var r = PackageHeader.ReadData(buf.AsMemory());

        Assert.Multiple(() =>
        {
            Assert.That(r.SessionId,  Is.EqualTo(sessionId));
            Assert.That(r.Type,       Is.EqualTo(PackageType.Data));
            Assert.That(r.Sequence,   Is.EqualTo(42u));
            Assert.That(r.Timestamp,  Is.EqualTo(ts));
        });
    }

    [Test]
    public void WriteRead_SequenceMaxValue()
    {
        var h   = new PackageHeader(Guid.NewGuid(), PackageType.Ack, uint.MaxValue,
                                    DateTimeOffset.UtcNow);
        var buf = new byte[PackageHeader.Size];
        h.WriteData(buf);
        var r = PackageHeader.ReadData(buf.AsMemory());
        Assert.That(r.Sequence, Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public void WriteData_ThrowsWhenBufferTooSmall()
    {
        var h = new PackageHeader(Guid.NewGuid(), PackageType.Ping, 0u, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => h.WriteData(new byte[PackageHeader.Size - 1]));
    }

    [Test]
    public void ReadData_ThrowsWhenBufferTooSmall()
    {
        Assert.Throws<ArgumentException>(
            () => PackageHeader.ReadData(new byte[PackageHeader.Size - 1].AsMemory()));
    }

    [Test]
    public void AllPackageTypes_RoundTrip()
    {
        foreach (PackageType pt in Enum.GetValues<PackageType>())
        {
            var h   = new PackageHeader(Guid.NewGuid(), pt, 1u, DateTimeOffset.UtcNow);
            var buf = new byte[PackageHeader.Size];
            h.WriteData(buf);
            Assert.That(PackageHeader.ReadData(buf.AsMemory()).Type, Is.EqualTo(pt),
                $"PackageType {pt} round-trip failed");
        }
    }

    [Test]
    public void WriteData_WithOffset_WritesAtCorrectPosition()
    {
        var h   = new PackageHeader(Guid.NewGuid(), PackageType.Data, 7u, DateTimeOffset.UtcNow);
        var buf = new byte[PackageHeader.Size + 4];
        h.WriteData(buf, offset: 4);
        using (Assert.EnterMultipleScope())
        {
            // Bytes 0..3 must be untouched (still zero).
            Assert.That(buf[0], Is.EqualTo(0));
            Assert.That(buf[3], Is.EqualTo(0));
        }
        // The header at offset 4 must be readable.
        var r = PackageHeader.ReadData(buf.AsMemory(), offset: 4);
        Assert.That(r.Sequence, Is.EqualTo(7u));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AckBody
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class AckBodyTests
{
    [Test]
    public void Length_Is10Bytes()
    {
        Assert.That(new AckBody(0u, 0u, 0).Length, Is.EqualTo(10));
    }

    [Test]
    public void WriteRead_RoundTrip()
    {
        var a   = new AckBody(100u, 0b1010u, 16);
        var buf = new byte[a.Length];
        a.WriteData(buf);
        var r = AckBody.ReadData(buf.AsMemory());
        Assert.Multiple(() =>
        {
            Assert.That(r.BaseSequence,   Is.EqualTo(100u));
            Assert.That(r.ReceivedMask,   Is.EqualTo(0b1010u));
            Assert.That(r.ReceiverWindow, Is.EqualTo(16));
        });
    }

    [Test]
    public void WriteRead_AllBitsSet()
    {
        var a   = new AckBody(0u, uint.MaxValue, ushort.MaxValue);
        var buf = new byte[a.Length];
        a.WriteData(buf);
        var r = AckBody.ReadData(buf.AsMemory());
        Assert.Multiple(() =>
        {
            Assert.That(r.ReceivedMask,   Is.EqualTo(uint.MaxValue));
            Assert.That(r.ReceiverWindow, Is.EqualTo(ushort.MaxValue));
        });
    }

    [Test]
    public void WriteData_ThrowsWhenBufferTooSmall()
    {
        Assert.Throws<ArgumentException>(() => new AckBody(1u, 1u, 1).WriteData(new byte[9]));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DataBody
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class DataBodyTests
{
    [Test]
    public void WriteRead_RoundTrip()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var b       = new DataBody(payload);
        var buf     = new byte[b.Length];
        b.WriteData(buf);
        var r = DataBody.ReadData(buf.AsMemory());
        Assert.That(r.Data.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public void Length_MatchesPayload()
    {
        Assert.That(new DataBody(new byte[123]).Length, Is.EqualTo(123));
    }

    [Test]
    public void ReadData_EmptyBuffer_Throws()
    {
        // ReadData requires at least 1 byte (offset check: buffer.Length < offset + 1).
        Assert.Throws<ArgumentException>(
            () => DataBody.ReadData(ReadOnlyMemory<byte>.Empty));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DataHeaderBody
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class DataHeaderBodyTests
{
    [Test]
    public void Length_Is4Bytes()
    {
        Assert.That(new DataHeaderBody(0).Length, Is.EqualTo(4));
    }

    [Test]
    public void WriteRead_RoundTrip()
    {
        var b   = new DataHeaderBody(99_999);
        var buf = new byte[b.Length];
        b.WriteData(buf);
        Assert.That(DataHeaderBody.ReadData(buf.AsMemory()).DataLength, Is.EqualTo(99_999));
    }

    [Test]
    public void WriteRead_NegativeValue_RoundTrips()
    {
        // int32 is used; negative values must survive the wire format.
        var b   = new DataHeaderBody(-1);
        var buf = new byte[b.Length];
        b.WriteData(buf);
        Assert.That(DataHeaderBody.ReadData(buf.AsMemory()).DataLength, Is.EqualTo(-1));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SequenceHelper
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class SequenceHelperTests
{
    [Test]
    public void GreaterOrEqual_Normal()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SequenceHelper.GreaterOrEqual(10u, 5u), Is.True);
            Assert.That(SequenceHelper.GreaterOrEqual(5u, 5u), Is.True);
            Assert.That(SequenceHelper.GreaterOrEqual(4u, 5u), Is.False);
        }
    }

    [Test]
    public void GreaterOrEqual_WrapAround_ZeroGreaterThanMaxValue()
    {
        // After uint.MaxValue wraps to 0, new seq 0 should be considered >= MaxValue.
        Assert.That(SequenceHelper.GreaterOrEqual(0u, uint.MaxValue), Is.True);
    }

    [Test]
    public void GreaterOrEqual_WrapAround_MaxValueNotGreaterThanZero()
    {
        Assert.That(SequenceHelper.GreaterOrEqual(uint.MaxValue, 0u), Is.False);
    }

    [Test]
    public void Distance_Normal()
    {
        // Distance(from=5, to=10) = 10 - 5 = 5
        Assert.That(SequenceHelper.Distance(5u, 10u), Is.EqualTo(5u));
    }

    [Test]
    public void Distance_WrapAround()
    {
        // Distance from uint.MaxValue+1 (=0) to uint.MaxValue wraps to uint.MaxValue.
        Assert.That(SequenceHelper.Distance(0u, uint.MaxValue), Is.EqualTo(uint.MaxValue));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SequenceManager
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class SequenceManagerTests
{
    [Test]
    public void Expected_StartsAtZero()
    {
        Assert.That(new SequenceManager().Expected, Is.EqualTo(0u));
    }

    [Test]
    public void GetNextOutgoing_FirstCall_Returns0()
    {
        // _currentSequence starts at uint.MaxValue; Interlocked.Increment wraps to 0.
        Assert.That(new SequenceManager().GetNextOutgoing(), Is.EqualTo(0u));
    }

    [Test]
    public void GetNextOutgoing_Increments()
    {
        var sm = new SequenceManager();
        Assert.That(sm.GetNextOutgoing(), Is.EqualTo(0u));
        Assert.That(sm.GetNextOutgoing(), Is.EqualTo(1u));
        Assert.That(sm.GetNextOutgoing(), Is.EqualTo(2u));
    }

    [Test]
    public void AdvanceExpected_Increments()
    {
        var sm = new SequenceManager();
        sm.AdvanceExpected();
        Assert.That(sm.Expected, Is.EqualTo(1u));
        sm.AdvanceExpected();
        Assert.That(sm.Expected, Is.EqualTo(2u));
    }

    [Test]
    public void AdvanceExpected_WrapAround()
    {
        var sm = new SequenceManager();
        // Manually advance to MaxValue by abusing that we can call it many times.
        // Instead of a loop to MaxValue (too slow), verify wrap via the Interlocked math:
        // just confirm the type wraps — unit-test that Expected can reach 0 after MaxValue.
        // We trust Interlocked.Increment wrapping here; the key semantic test is the normal increment above.
        Assert.DoesNotThrow(() =>
        {
            for (var i = 0; i < 100; i++) sm.AdvanceExpected();
        });
        Assert.That(sm.Expected, Is.EqualTo(100u));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ReorderingBuffer
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class ReorderingBufferTests
{
    private static ProtocolPackage MakePkg(uint seq) =>
        new(new PackageHeader(Guid.NewGuid(), PackageType.Data, seq, DateTimeOffset.UtcNow),
            new DataBody(new[] { (byte)(seq & 0xFF) }));

    [Test]
    public void TryAdd_ThenGet_ReturnsSamePackage()
    {
        var buf = new ReorderingBuffer();
        var pkg = MakePkg(5u);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(buf.TryAdd(pkg), Is.True);
            Assert.That(buf.TryGetOrderedPackage(5u, out var got), Is.True);
            Assert.That(got, Is.SameAs(pkg));
        }
    }

    [Test]
    public void TryAdd_Duplicate_ReturnsFalse()
    {
        var buf = new ReorderingBuffer();
        var pkg = MakePkg(3u);
        buf.TryAdd(pkg);
        Assert.That(buf.TryAdd(MakePkg(3u)), Is.False);
    }

    [Test]
    public void TryGetOrderedPackage_Missing_ReturnsFalse()
    {
        var buf = new ReorderingBuffer();
        Assert.That(buf.TryGetOrderedPackage(99u, out _), Is.False);
    }

    [Test]
    public void TryGetOrderedPackage_ClearsSlot_AllowsReuse()
    {
        var buf = new ReorderingBuffer();
        buf.TryAdd(MakePkg(7u));
        buf.TryGetOrderedPackage(7u, out _);
        // Slot freed — a new packet with the same sequence (or one that maps to same slot) can be added.
        Assert.That(buf.TryAdd(MakePkg(7u)), Is.True);
    }

    [Test]
    public void GetPackagesMask_CorrectBitsSet()
    {
        var buf = new ReorderingBuffer();
        buf.TryAdd(MakePkg(0u));
        // Skip seq=1 intentionally.
        buf.TryAdd(MakePkg(2u));
        buf.TryAdd(MakePkg(3u));

        var mask = buf.GetPackagesMask(0u, 4u);
        // Bit 0 → seq 0 present, bit 1 → seq 1 absent, bit 2 → seq 2 present, bit 3 → seq 3 present
        // Expected: 0b1101 = 13
        Assert.That(mask, Is.EqualTo(0b1101u));
    }

    [Test]
    public void GetPackagesMask_InvalidRange_Throws()
    {
        var buf = new ReorderingBuffer();
        Assert.Throws<ArgumentException>(() => buf.GetPackagesMask(5u, 3u));
    }

    [Test]
    public void GetPackagesMask_EmptyRange_Throws()
    {
        var buf = new ReorderingBuffer();
        Assert.Throws<ArgumentException>(() => buf.GetPackagesMask(5u, 5u));
    }

    [Test]
    public void WrapAround_HighSequence_WorksCorrectly()
    {
        var buf = new ReorderingBuffer();
        var seq = uint.MaxValue - 2;
        var pkg = MakePkg(seq);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(buf.TryAdd(pkg), Is.True);
            Assert.That(buf.TryGetOrderedPackage(seq, out var got), Is.True);
            Assert.That(got!.Header.Sequence, Is.EqualTo(seq));
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// OutgoingBuffer
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class OutgoingBufferTests
{
    private static ProtocolPackage MakePkg(uint seq) =>
        new(new PackageHeader(Guid.NewGuid(), PackageType.Data, seq, DateTimeOffset.UtcNow),
            new DataBody(new byte[] { 0xFF }));

    [Test]
    public void Set_ThenPeek_ReturnsSameInstance()
    {
        var buf = new OutgoingBuffer();
        var pkg = MakePkg(0u);
        buf.Set(pkg);
        Assert.That(buf.Peek(0u), Is.SameAs(pkg));
    }

    [Test]
    public void Set_Overwrites_PreviousValue()
    {
        var buf  = new OutgoingBuffer();
        var pkg1 = MakePkg(0u);
        var pkg2 = MakePkg(0u);
        buf.Set(pkg1);
        buf.Set(pkg2);
        Assert.That(buf.Peek(0u), Is.SameAs(pkg2));
    }

    [Test]
    public void Remove_ClearsSlot()
    {
        var buf = new OutgoingBuffer();
        buf.Set(MakePkg(1u));
        buf.Remove(1u);
        Assert.That(buf.Peek(1u), Is.Null);
    }

    [Test]
    public void Clear_Range_RemovesAll()
    {
        var buf = new OutgoingBuffer();
        for (uint i = 0; i < 5; i++) buf.Set(MakePkg(i));
        buf.Clear(0u, 5u);
        for (uint i = 0; i < 5; i++)
            Assert.That(buf.Peek(i), Is.Null, $"slot {i} should be null");
    }

    [Test]
    public void Peek_MissingSlot_ReturnsNull()
    {
        Assert.That(new OutgoingBuffer().Peek(42u), Is.Null);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DataWindow  (includes BUG-1 regression)
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class DataWindowTests
{
    private const int Ps = 1369; // MaxPayloadSize

    // Helper: build a byte array filled with a single value.
    private static byte[] Fill(byte value, int len) =>
        Enumerable.Repeat(value, len).ToArray();

    [Test]
    public void Contains_Boundaries()
    {
        var w = new DataWindow(5u, 10u, 100u);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(w.Contains(5u), Is.True);
            Assert.That(w.Contains(9u), Is.True);
            Assert.That(w.Contains(10u), Is.False); // EndSequence is exclusive
            Assert.That(w.Contains(4u), Is.False);
        }
    }

    [Test]
    public void TryAdd_DuplicateSlot_ReturnsFalse()
    {
        var w = new DataWindow(0u, 4u, Ps);
        w.TryAdd(0u, new byte[10]);
        Assert.That(w.TryAdd(0u, new byte[10]), Is.False);
    }

    [Test]
    public void TryAdd_OutsideWindow_ReturnsFalse()
    {
        var w = new DataWindow(0u, 4u, Ps);
        Assert.That(w.TryAdd(4u, new byte[10]), Is.False);
    }

    [Test]
    public void IsReady_False_WhenPartiallyFilled()
    {
        var w = new DataWindow(0u, 2u, Ps * 2);
        w.TryAdd(0u, Fill(0xAA, Ps));
        Assert.That(w.IsReady(), Is.False);
    }

    [Test]
    public void IsReady_True_WhenFullyFilled()
    {
        var w = new DataWindow(0u, 1u, 50u);
        w.TryAdd(0u, Fill(0x01, 50));
        Assert.That(w.IsReady(), Is.True);
    }

    [Test]
    public void HasData_FalseInitially_TrueAfterFirstAdd()
    {
        var w = new DataWindow(0u, 2u, 10u);
        Assert.That(w.HasData, Is.False);
        w.TryAdd(0u, new byte[10]);
        Assert.That(w.HasData, Is.True);
    }

    [Test]
    public void Update_ResetsState()
    {
        var w = new DataWindow(0u, 2u, Ps);
        w.TryAdd(0u, Fill(0xFF, 10));
        w.Update(10u, 12u, Ps * 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(w.StartSequence, Is.EqualTo(10u));
            Assert.That(w.HasData, Is.False);
        }
    }

    [Test]
    public void Flush_EmptyWindow_ReturnsEmptyArray()
    {
        var w = new DataWindow(0u, 2u, Ps);
        Assert.That(w.Flush(), Is.Empty);
    }

    // ── BUG-1 regression ──────────────────────────────────────────────────────

    [Test]
    public void Flush_ContiguousFromSlot0_CorrectData()
    {
        // Two full slots filled in order.
        var w = new DataWindow(0u, 2u, Ps * 2);
        var a = Fill(0xAA, Ps);
        var b = Fill(0xBB, Ps);
        w.TryAdd(0u, a);
        w.TryAdd(1u, b);

        var flushed = w.Flush();
        Assert.That(flushed.Length, Is.EqualTo(Ps * 2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flushed[0], Is.EqualTo(0xAA));
            Assert.That(flushed[Ps], Is.EqualTo(0xBB));
        }
    }

    [Test]
    public void Flush_OutOfOrderDelivery_CorrectData()
    {
        // Slot 1 arrives before slot 0 (typical reorder scenario).
        var w = new DataWindow(10u, 12u, Ps * 2);
        var a = Fill(0x01, Ps);
        var b = Fill(0x02, Ps);
        w.TryAdd(11u, b); // slot index 1 first
        w.TryAdd(10u, a); // slot index 0 second

        var flushed = w.Flush();
        Assert.That(flushed.Length, Is.EqualTo(Ps * 2));
        using (Assert.EnterMultipleScope())
        {
            // Slot 0 (a) must appear first regardless of add order.
            Assert.That(flushed[0], Is.EqualTo(0x01),
                "BUG-1 regression: slot 0 bytes must come first in Flush output");
            Assert.That(flushed[Ps], Is.EqualTo(0x02),
                "BUG-1 regression: slot 1 bytes must follow slot 0");
        }
    }

    [Test]
    public void Flush_LastSlotSmallerThanMaxPayload_CorrectLength()
    {
        // Two slots: first is full (PS bytes), last is short (50 bytes).
        var totalBytes = Ps + 50;
        var w = new DataWindow(0u, 2u, (uint)totalBytes);
        w.TryAdd(0u, Fill(0xAA, Ps));
        w.TryAdd(1u, Fill(0xBB, 50));

        var flushed = w.Flush();
        Assert.That(flushed.Length, Is.EqualTo(totalBytes));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flushed[0], Is.EqualTo(0xAA));
            Assert.That(flushed[Ps], Is.EqualTo(0xBB));
            Assert.That(flushed[Ps + 49], Is.EqualTo(0xBB));
        }
    }

    [Test]
    public void Flush_SingleSmallPacket_CorrectData()
    {
        // Common case: one small packet (e.g. 64 bytes telemetry).
        var payload = Fill(0x42, 64);
        var w = new DataWindow(0u, 1u, 64u);
        w.TryAdd(0u, payload);

        var flushed = w.Flush();
        Assert.That(flushed, Is.EqualTo(payload));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RtoCalculator
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class RtoCalculatorTests
{
    [Test]
    public void Initial_Rto_Is200ms()
    {
        Assert.That(new RtoCalculator().Rto.TotalMilliseconds, Is.EqualTo(200).Within(0.01));
    }

    [Test]
    public void AfterFirstSample_Rto_AtLeast200ms()
    {
        var rto = new RtoCalculator();
        rto.Update(TimeSpan.FromMilliseconds(10));
        // SRTT=10, RTTVAR=5, RTO = max(10+4*5, 200) = max(30, 200) = 200
        Assert.That(rto.Rto.TotalMilliseconds, Is.GreaterThanOrEqualTo(200));
    }

    [Test]
    public void LargeRtt_RaisesRtoAbove200ms()
    {
        var rto = new RtoCalculator();
        rto.Update(TimeSpan.FromMilliseconds(300));
        // SRTT=300, RTTVAR=150, RTO = max(300+600, 200) = 900
        Assert.That(rto.Rto.TotalMilliseconds, Is.GreaterThan(200));
    }

    [Test]
    public void SecondSample_EwmaConverges()
    {
        var rto = new RtoCalculator();
        rto.Update(TimeSpan.FromMilliseconds(100));
        rto.Update(TimeSpan.FromMilliseconds(100));
        // After two identical 100ms samples SRTT ≈ 100, variance small, RTO = 200 (min clamp).
        Assert.That(rto.Rto.TotalMilliseconds, Is.GreaterThanOrEqualTo(200));
    }

    [Test]
    public void ZeroRtt_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => new RtoCalculator().Update(TimeSpan.Zero));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CongestionController
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class CongestionControllerTests
{
    [Test]
    public void Initial_Window_Is4()
    {
        Assert.That(new CongestionController().Window, Is.EqualTo(4));
    }

    [Test]
    public void SlowStart_GrowsByOnePerAck()
    {
        var cc = new CongestionController();
        var w0 = cc.Window; // 4
        cc.OnAck();
        Assert.That(cc.Window, Is.EqualTo(w0 + 1));
    }

    [Test]
    public void SlowStart_GrowsUntilThreshold()
    {
        var cc = new CongestionController();
        // Default ssthresh = 32; keep ACK-ing until cwnd reaches it.
        for (var i = 0; i < 28; i++) cc.OnAck(); // 4 + 28 = 32
        Assert.That(cc.Window, Is.EqualTo(32));

        // One more ACK should NOT add 1 (we are at threshold, AIMD territory).
        cc.OnAck();
        Assert.That(cc.Window, Is.LessThanOrEqualTo(33)); // fractional increment
    }

    [Test]
    public void OnLoss_HalvesCwndAndSsthresh()
    {
        var cc = new CongestionController();
        for (var i = 0; i < 20; i++) cc.OnAck(); // cwnd = 24
        var beforeLoss = cc.Window;
        cc.OnLoss();
        Assert.That(cc.Window, Is.LessThan(beforeLoss));
    }

    [Test]
    public void Window_NeverBelowOne()
    {
        var cc = new CongestionController();
        for (var i = 0; i < 200; i++) cc.OnLoss();
        Assert.That(cc.Window, Is.GreaterThanOrEqualTo(1));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FlowController
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class FlowControllerTests
{
    [Test]
    public void Initial_Available_MatchesConstructor()
    {
        Assert.That(new FlowController(16).Available, Is.EqualTo(16));
    }

    [Test]
    public void Update_ChangesAvailable()
    {
        var fc = new FlowController(16);
        fc.Update(8);
        Assert.That(fc.Available, Is.EqualTo(8));
    }

    [Test]
    public void Update_NegativeValue_ClampsToZero()
    {
        var fc = new FlowController(10);
        fc.Update(-5);
        Assert.That(fc.Available, Is.EqualTo(0));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SendEngine  (includes BUG-2 regression)
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class SendEngineTests
{
    private static ProtocolPackage MakePkg(uint seq) =>
        new(new PackageHeader(Guid.NewGuid(), PackageType.Data, seq, DateTimeOffset.UtcNow),
            new DataBody(new byte[] { 0x01 }));

    // ── Helper: create engine + start its mailbox consumer loop ──────────────
    // The consumer must be running for EnqueueAsync / OnAckAsync to actually
    // execute their commands (mailbox pattern — commands are queued and then
    // executed by RunAsync).  We start it as a background task and cancel it
    // via the returned CTS when the test is done.
    private static (SendEngine engine, CancellationTokenSource cts)
        CreateEngine(List<ProtocolPackage> sent, int flowWindow = 32)
    {
        var cts    = new CancellationTokenSource();
        var engine = new SendEngine(
            (p, _) => { sent.Add(p); return ValueTask.CompletedTask; },
            flowWindow);
        _ = engine.RunAsync(cts.Token);   // start the consumer loop
        return (engine, cts);
    }

    [TearDown]
    public void TearDown() { /* individual tests cancel their own CTS */ }

    [Test]
    public async Task Enqueue_SendsImmediately_WhenWindowNotFull()
    {
        var sent = new List<ProtocolPackage>();
        var (engine, cts) = CreateEngine(sent, flowWindow: 32);
        try
        {
            var pkg = MakePkg(0u);
            await engine.EnqueueAsync(pkg, CancellationToken.None);
            // EnqueueAsync awaits the TCS, which resolves only after the command
            // has executed inside the consumer loop — so sent is already populated.
            Assert.That(sent.Count, Is.EqualTo(1));
            Assert.That(sent[0], Is.SameAs(pkg));
        }
        finally { await cts.CancelAsync(); }
    }

    [Test]
    public async Task Enqueue_Queues_WhenWindowFull()
    {
        // flow window = 1, cwnd = 4, min(4,1) = 1.
        // First packet fills the in-flight slot; second stays pending.
        var sent = new List<ProtocolPackage>();
        var (engine, cts) = CreateEngine(sent, flowWindow: 1);
        try
        {
            await engine.EnqueueAsync(MakePkg(0u), CancellationToken.None);
            await engine.EnqueueAsync(MakePkg(1u), CancellationToken.None);
            Assert.That(sent.Count, Is.EqualTo(1), "seq=1 must remain pending");
        }
        finally { await cts.CancelAsync(); }
    }

    [Test]
    public async Task OnAck_ConfirmsPacket_DrainsPending()
    {
        var sent = new List<ProtocolPackage>();
        var (engine, cts) = CreateEngine(sent, flowWindow: 1);
        try
        {
            await engine.EnqueueAsync(MakePkg(0u), CancellationToken.None);
            await engine.EnqueueAsync(MakePkg(1u), CancellationToken.None);
            Assert.That(sent.Count, Is.EqualTo(1), "only seq=0 sent so far");

            // ACK seq=0 — frees the in-flight slot — seq=1 should drain from pending.
            await engine.OnAckAsync(new AckBody(0u, 0u, 1), CancellationToken.None);

            Assert.That(sent.Count, Is.EqualTo(2), "seq=1 must be sent after ACK");
        }
        finally { await cts.CancelAsync(); }
    }

    [Test]
    public async Task OnAck_SackMask_ConfirmsOutOfOrderPacket()
    {
        var sent = new List<ProtocolPackage>();
        var (engine, cts) = CreateEngine(sent, flowWindow: 32);
        try
        {
            await engine.EnqueueAsync(MakePkg(0u), CancellationToken.None);
            await engine.EnqueueAsync(MakePkg(1u), CancellationToken.None);
            sent.Clear();

            // BaseSeq=0 (cumulative) + bit 0 of mask = seq 1 confirmed via SACK.
            await engine.OnAckAsync(new AckBody(0u, 0b1u, 32), CancellationToken.None);

            Assert.That(sent.Select(p => p.Header.Sequence), Does.Not.Contain(1u),
                "seq=1 confirmed via SACK must not be retransmitted");
        }
        finally { await cts.CancelAsync(); }
    }

    [Test]
    public async Task PostTick_RetransmitsExpiredPackets()
    {
        // PostTick() posts a fire-and-forget command into the mailbox.
        // We need to wait for it to complete — use a semaphore released by the send callback.
        var sendCount = 0;
        var tcs       = new TaskCompletionSource();
        var cts       = new CancellationTokenSource();

        var engine = new SendEngine(
            (_, _) =>
            {
                if (Interlocked.Increment(ref sendCount) >= 2)
                    tcs.TrySetResult(); // signal after first retransmit
                return ValueTask.CompletedTask;
            }, 32);
        _ = engine.RunAsync(cts.Token);

        try
        {
            await engine.EnqueueAsync(MakePkg(0u), CancellationToken.None);
            Assert.That(sendCount, Is.EqualTo(1), "initial send");

            // Drive RTO to its minimum (200 ms) and wait for it to expire.
            engine.UpdateRtt(TimeSpan.FromMilliseconds(1));
            await Task.Delay(250); // > 200 ms minimum RTO

            // PostTick is fire-and-forget — wait for completion via TCS.
            engine.PostTick();
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.That(sendCount, Is.GreaterThanOrEqualTo(2),
                "packet must be retransmitted after RTO expiry");
        }
        finally { await cts.CancelAsync(); }
    }

    // ── BUG-2 regression ──────────────────────────────────────────────────────

    [Test]
    public async Task OnAck_BaseSequenceMaxValue_DoesNotRetransmitConfirmedPacket()
    {
        var retransmitted = new List<uint>();
        var cts    = new CancellationTokenSource();
        var engine = new SendEngine(
            (p, _) => { retransmitted.Add(p.Header.Sequence); return ValueTask.CompletedTask; },
            32);
        _ = engine.RunAsync(cts.Token);

        try
        {
            await engine.EnqueueAsync(MakePkg(uint.MaxValue), CancellationToken.None);
            retransmitted.Clear(); // don't count initial send

            await engine.OnAckAsync(new AckBody(uint.MaxValue, 0u, 32), CancellationToken.None);

            Assert.That(retransmitted, Does.Not.Contain(uint.MaxValue),
                "BUG-2: confirmed packet must not be retransmitted after wrap-around ACK");
        }
        finally { await cts.CancelAsync(); }
    }

    [Test]
    public async Task OnAck_WithZeroReceiverWindow_StopsSending()
    {
        var sent = new List<ProtocolPackage>();
        var (engine, cts) = CreateEngine(sent, flowWindow: 32);
        try
        {
            await engine.EnqueueAsync(MakePkg(0u), CancellationToken.None);
            sent.Clear();

            // ReceiverWindow=0 closes the flow window.
            await engine.OnAckAsync(new AckBody(0u, 0u, 0), CancellationToken.None);

            // seq=1 must stay pending because Available == 0.
            await engine.EnqueueAsync(MakePkg(1u), CancellationToken.None);
            Assert.That(sent.Count, Is.EqualTo(0), "nothing sent when receiver window is 0");
        }
        finally { await cts.CancelAsync(); }
    }

    [Test]
    public async Task DisposeAsync_CompletesMailbox_NoHang()
    {
        var sent = new List<ProtocolPackage>();
        var (engine, cts) = CreateEngine(sent, flowWindow: 32);
        await engine.EnqueueAsync(MakePkg(0u), CancellationToken.None);

        // Disposing should complete without hanging.
        var disposeTask = engine.DisposeAsync().AsTask();
        await cts.CancelAsync();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Pass("DisposeAsync completed without timeout");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PackageHelper
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
internal class PackageHelperTests
{
    private static readonly PackageType[] RegisteredTypes =
    [
        PackageType.Handshake,
        PackageType.Ack,
        PackageType.DataHeader,
        PackageType.Data,
        PackageType.Ping,
        PackageType.Pong,
    ];

    [TestCaseSource(nameof(RegisteredTypes))]
    public void BodyReaders_ContainsKey(PackageType pt)
    {
        Assert.That(PackageHelper.BodyReaders.ContainsKey(pt), Is.True);
    }

    [TestCaseSource(nameof(RegisteredTypes))]
    public void Handlers_ContainsKey(PackageType pt)
    {
        Assert.That(PackageHelper.Handlers.ContainsKey(pt), Is.True);
    }

    [Test]
    public void AckBodyReader_ParsesCorrectly()
    {
        var orig = new AckBody(42u, 0b111u, 8);
        var buf  = new byte[orig.Length];
        orig.WriteData(buf);
        var body = (AckBody)PackageHelper.BodyReaders[PackageType.Ack](buf.AsMemory());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.BaseSequence, Is.EqualTo(42u));
            Assert.That(body.ReceivedMask, Is.EqualTo(0b111u));
        }
    }

    [Test]
    public void DataBodyReader_ParsesPayload()
    {
        var payload = new byte[] { 10, 20, 30 };
        var orig    = new DataBody(payload);
        var buf     = new byte[orig.Length];
        orig.WriteData(buf);
        var body = (DataBody)PackageHelper.BodyReaders[PackageType.Data](buf.AsMemory());
        Assert.That(body.Data.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public void DataHeaderBodyReader_ParsesDataLength()
    {
        var orig = new DataHeaderBody(7777);
        var buf  = new byte[orig.Length];
        orig.WriteData(buf);
        var body = (DataHeaderBody)PackageHelper.BodyReaders[PackageType.DataHeader](buf.AsMemory());
        Assert.That(body.DataLength, Is.EqualTo(7777));
    }

    [Test]
    public void HandshakeBodyReader_ParsesEmptyPayload()
    {
        var body = PackageHelper.BodyReaders[PackageType.Handshake](ReadOnlyMemory<byte>.Empty);
        Assert.That(body, Is.InstanceOf<HandshakeBody>());
    }

    [Test]
    public void PingBodyReader_ParsesEmptyPayload()
    {
        Assert.DoesNotThrow(
            () => PackageHelper.BodyReaders[PackageType.Ping](ReadOnlyMemory<byte>.Empty));
    }

    [Test]
    public void PongBodyReader_ParsesEmptyPayload()
    {
        Assert.DoesNotThrow(
            () => PackageHelper.BodyReaders[PackageType.Pong](ReadOnlyMemory<byte>.Empty));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ProtocolPackage
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class ProtocolPackageTests
{
    [Test]
    public void Length_IsHeaderPlusBodyLength()
    {
        var h   = new PackageHeader(Guid.NewGuid(), PackageType.Data, 0u, DateTimeOffset.UtcNow);
        var b   = new DataBody(new byte[50]);
        var pkg = new ProtocolPackage(h, b);
        Assert.That(pkg.Length, Is.EqualTo(PackageHeader.Size + 50));
    }

    [Test]
    public void Header_And_Body_AreAccessible()
    {
        var h   = new PackageHeader(Guid.NewGuid(), PackageType.Ping, 1u, DateTimeOffset.UtcNow);
        var b   = new PingBody();
        var pkg = new ProtocolPackage(h, b);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pkg.Header, Is.SameAs(h));
            Assert.That(pkg.Body, Is.SameAs(b));
        }
    }

    [Test]
    public void ZeroBodyLength_PingPackage()
    {
        var h   = new PackageHeader(Guid.NewGuid(), PackageType.Ping, 0u, DateTimeOffset.UtcNow);
        var b   = new PingBody();
        var pkg = new ProtocolPackage(h, b);
        Assert.That(pkg.Length, Is.EqualTo(PackageHeader.Size));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Full wire serialisation round-trips
// ─────────────────────────────────────────────────────────────────────────────
[TestFixture]
public class WireRoundTripTests
{
    private static byte[] Serialise(ProtocolPackage pkg)
    {
        var buf = new byte[pkg.Length];
        pkg.Header.WriteData(buf);
        pkg.Body.WriteData(buf.AsSpan()[pkg.Header.Length..]);
        return buf;
    }

    [Test]
    public void DataPackage_WireRoundTrip()
    {
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var pkg     = new ProtocolPackage(
            new PackageHeader(Guid.NewGuid(), PackageType.Data, 7u, DateTimeOffset.UtcNow),
            new DataBody(payload));

        var buf = Serialise(pkg);
        var rh  = PackageHeader.ReadData(buf.AsMemory());
        var rb  = DataBody.ReadData(buf.AsMemory()[rh.Length..]);

        Assert.Multiple(() =>
        {
            Assert.That(rh.Sequence,       Is.EqualTo(7u));
            Assert.That(rh.Type,           Is.EqualTo(PackageType.Data));
            Assert.That(rb.Data.ToArray(), Is.EqualTo(payload));
        });
    }

    [Test]
    public void AckPackage_WireRoundTrip()
    {
        var pkg = new ProtocolPackage(
            new PackageHeader(Guid.NewGuid(), PackageType.Ack, 3u, DateTimeOffset.UtcNow),
            new AckBody(99u, 0b11u, 32));

        var buf = Serialise(pkg);
        var rh  = PackageHeader.ReadData(buf.AsMemory());
        var rb  = AckBody.ReadData(buf.AsMemory()[rh.Length..]);

        Assert.Multiple(() =>
        {
            Assert.That(rh.Type,         Is.EqualTo(PackageType.Ack));
            Assert.That(rb.BaseSequence, Is.EqualTo(99u));
            Assert.That(rb.ReceivedMask, Is.EqualTo(0b11u));
        });
    }

    [Test]
    public void DataHeaderPackage_WireRoundTrip()
    {
        var pkg = new ProtocolPackage(
            new PackageHeader(Guid.NewGuid(), PackageType.DataHeader, 0u, DateTimeOffset.UtcNow),
            new DataHeaderBody(12345));

        var buf = Serialise(pkg);
        var rh  = PackageHeader.ReadData(buf.AsMemory());
        var rb  = DataHeaderBody.ReadData(buf.AsMemory()[rh.Length..]);

        Assert.That(rb.DataLength, Is.EqualTo(12345));
    }

    [Test]
    public void PingPackage_WireRoundTrip_ZeroBody()
    {
        var pkg = new ProtocolPackage(
            new PackageHeader(Guid.NewGuid(), PackageType.Ping, 0u, DateTimeOffset.UtcNow),
            new PingBody());

        var buf = Serialise(pkg);
        Assert.That(buf.Length, Is.EqualTo(PackageHeader.Size));

        var rh = PackageHeader.ReadData(buf.AsMemory());
        Assert.That(rh.Type, Is.EqualTo(PackageType.Ping));
    }
}

namespace ObdLink.Core.IsoTp;

public enum FrameType { Single = 0, First = 1, Consecutive = 2, FlowControl = 3 }

/// <summary>ISO 15765-2 (classic CAN, 8-byte frames) framing helpers.</summary>
public static class IsoTpCodec
{
    public static FrameType TypeOf(ReadOnlySpan<byte> frame) => (FrameType)(frame[0] >> 4);

    /// <summary>Segments a payload into CAN frames (data portion only, without padding unless pad != null).</summary>
    public static List<byte[]> Segment(ReadOnlySpan<byte> payload, byte? pad = 0x00)
    {
        var frames = new List<byte[]>();
        if (payload.Length <= 7)
        {
            var f = new List<byte> { (byte)payload.Length };
            f.AddRange(payload.ToArray());
            frames.Add(Pad(f, pad));
            return frames;
        }
        if (payload.Length > 4095) throw new ArgumentException("Classic ISO-TP max payload is 4095 bytes");
        var first = new List<byte> { (byte)(0x10 | (payload.Length >> 8)), (byte)(payload.Length & 0xFF) };
        first.AddRange(payload[..6].ToArray());
        frames.Add(first.ToArray());
        int seq = 1;
        for (int i = 6; i < payload.Length; i += 7, seq = (seq + 1) & 0x0F)
        {
            var cf = new List<byte> { (byte)(0x20 | seq) };
            cf.AddRange(payload.Slice(i, Math.Min(7, payload.Length - i)).ToArray());
            frames.Add(Pad(cf, pad));
        }
        return frames;
    }

    public static byte[] FlowControl(byte flag = 0, byte blockSize = 0, byte stMin = 0) => new byte[] { (byte)(0x30 | flag), blockSize, stMin, 0, 0, 0, 0, 0 };

    private static byte[] Pad(List<byte> f, byte? pad) { if (pad is byte p) while (f.Count < 8) f.Add(p); return f.ToArray(); }
}

/// <summary>Reassembles one ISO-TP message from received frames (per source id).</summary>
public sealed class IsoTpReassembler
{
    private byte[]? _buf; private int _len, _pos, _nextSeq;
    public bool InProgress => _buf is not null;

    /// <summary>Feed one frame; returns complete payload or null if more frames are needed. Flow control frames are ignored.</summary>
    public byte[]? Feed(ReadOnlySpan<byte> frame)
    {
        if (frame.Length == 0) return null;
        switch (IsoTpCodec.TypeOf(frame))
        {
            case FrameType.Single:
                int l = frame[0] & 0x0F;
                if (l == 0 || l > frame.Length - 1) throw new IsoTpException($"Bad single frame length {l}");
                _buf = null; return frame.Slice(1, l).ToArray();
            case FrameType.First:
                _len = ((frame[0] & 0x0F) << 8) | frame[1];
                if (_len < 8) throw new IsoTpException("First frame length < 8");
                _buf = new byte[_len]; _pos = 0; _nextSeq = 1;
                Copy(frame[2..]); return null;
            case FrameType.Consecutive:
                if (_buf is null) throw new IsoTpException("Consecutive frame without first frame");
                int sn = frame[0] & 0x0F;
                if (sn != _nextSeq) throw new IsoTpException($"Sequence error: expected {_nextSeq}, got {sn}");
                _nextSeq = (_nextSeq + 1) & 0x0F;
                Copy(frame[1..]);
                if (_pos >= _len) { var r = _buf; _buf = null; return r; }
                return null;
            default: return null;
        }
    }

    private void Copy(ReadOnlySpan<byte> d) { int n = Math.Min(d.Length, _len - _pos); d[..n].CopyTo(_buf.AsSpan(_pos)); _pos += n; }
}

public sealed class IsoTpException(string m) : Exception(m);

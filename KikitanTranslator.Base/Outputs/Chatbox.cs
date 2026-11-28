using System.IO;
using System.Net.Sockets;
using System.Text;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base.Outputs;

public class Chatbox : IOutput
{
    private static void SendOscPacket(byte[] packet, string address, int port)
    {
        if (port <= 0 || port > 65535) return;
        try
        {
            using var client = new UdpClient();
            client.Send(packet, packet.Length, address, port);
        }
        catch (Exception ex)
        {
            Log.Warning($"[OSC] Failed to send UDP packet to {address}:{port}: {ex.Message}");
        }
    }

    private static byte[] BuildChatboxInputPacket(string message, bool direct = true, bool complete = true)
    {
        using var ms = new MemoryStream();

        // 1. Address: "/chatbox/input"
        byte[] addrBytes = Encoding.ASCII.GetBytes("/chatbox/input");
        ms.Write(addrBytes, 0, addrBytes.Length);
        ms.WriteByte(0);
        while (ms.Position % 4 != 0) ms.WriteByte(0);

        // 2. Type tags: ",sTT" (string, direct=T, complete=T)
        string tags = ",s" + (direct ? "T" : "F") + (complete ? "T" : "F");
        byte[] tagBytes = Encoding.ASCII.GetBytes(tags);
        ms.Write(tagBytes, 0, tagBytes.Length);
        ms.WriteByte(0);
        while (ms.Position % 4 != 0) ms.WriteByte(0);

        // 3. Message data: UTF-8 string null-terminated and 4-byte aligned
        byte[] msgBytes = Encoding.UTF8.GetBytes(message);
        ms.Write(msgBytes, 0, msgBytes.Length);
        ms.WriteByte(0);
        while (ms.Position % 4 != 0) ms.WriteByte(0);

        return ms.ToArray();
    }

    private static byte[] BuildTypingPacket(bool isTyping)
    {
        using var ms = new MemoryStream();

        // 1. Address: "/chatbox/typing"
        byte[] addrBytes = Encoding.ASCII.GetBytes("/chatbox/typing");
        ms.Write(addrBytes, 0, addrBytes.Length);
        ms.WriteByte(0);
        while (ms.Position % 4 != 0) ms.WriteByte(0);

        // 2. Type tag: ",T" or ",F"
        string tags = isTyping ? ",T" : ",F";
        byte[] tagBytes = Encoding.ASCII.GetBytes(tags);
        ms.Write(tagBytes, 0, tagBytes.Length);
        ms.WriteByte(0);
        while (ms.Position % 4 != 0) ms.WriteByte(0);

        return ms.ToArray();
    }

    public void Send(string recognized, string translated, bool final)
    {
        try
        {
            var targetHost = AppConfig.ConfigObject.OscAddress;
            var primaryPort = AppConfig.ConfigObject.OscPort;
            var secondaryPort = AppConfig.ConfigObject.OscSecondaryPort;
            var dualDispatch = AppConfig.ConfigObject.OscEnableDualDispatch || (secondaryPort > 0 && secondaryPort != primaryPort);

            // 1. Typing indicator
            var typingPacket = BuildTypingPacket(!final);
            SendOscPacket(typingPacket, targetHost, primaryPort);
            if (dualDispatch && secondaryPort > 0)
            {
                SendOscPacket(typingPacket, targetHost, secondaryPort);
            }

            if (!final && !AppConfig.ConfigObject.SendWithoutWaitingForFinish) return;

            string messageToSend = FormatChatboxMessage(recognized, translated);
            if (string.IsNullOrWhiteSpace(messageToSend)) return;

            var inputPacket = BuildChatboxInputPacket(messageToSend, true, true);
            SendOscPacket(inputPacket, targetHost, primaryPort);
            if (dualDispatch && secondaryPort > 0)
            {
                SendOscPacket(inputPacket, targetHost, secondaryPort);
                Log.Debug($"[OSC] Chatbox dual dispatch to {targetHost}:{secondaryPort}");
            }

            Log.Debug($"[OSC] Chatbox send completed: host={targetHost}, port={primaryPort}, final={final}, len={messageToSend.Length}");
        }
        catch (Exception e)
        {
            Log.Error($"[OSC] Error sending OSC message! Reason: {e}");
        }
    }

    private const int MaxChatboxLength = 144;

    /// <summary>
    /// Enforces VRChat's 144-character /chatbox/input limit while prioritizing the full translation
    /// so long bilingual sentences are never silently rejected or truncated mid-translation.
    /// </summary>
    private static string FormatChatboxMessage(string recognized, string translated)
    {
        string orig = (recognized ?? string.Empty).Replace("\r", "").Replace("\n", " ").Trim();
        string trans = (translated ?? string.Empty).Replace("\r", "").Replace("\n", " ").Trim();

        if (AppConfig.ConfigObject.SpeechToTextOnly) return TrimToLimit(orig, MaxChatboxLength);
        if (AppConfig.ConfigObject.TranslationOnly || string.IsNullOrEmpty(orig)) return TrimToLimit(trans, MaxChatboxLength);
        if (string.IsNullOrEmpty(trans)) return TrimToLimit(orig, MaxChatboxLength);

        string combined = $"{trans} ({orig})";
        if (combined.Length <= MaxChatboxLength) return combined;

        // Preserve full translation first; trim original parenthetical to fit within 144 chars
        int overhead = 3; // " (" + ")"
        int remainingForOrig = MaxChatboxLength - trans.Length - overhead;
        if (remainingForOrig >= 6)
        {
            return $"{trans} ({TrimToLimit(orig, remainingForOrig)})";
        }

        return TrimToLimit(trans, MaxChatboxLength);
    }

    private static string TrimToLimit(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
        if (maxLen <= 1) return text[..Math.Max(0, maxLen)];
        return text[..(maxLen - 1)].TrimEnd() + "…";
    }

    public bool IsDelayed() => true;
}
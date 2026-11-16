using System.Text;
namespace UnscRelay.Core;

public static class RecordingText
{
    // Transcript, not a terminal-screen emulation. Escape sequences never reach the text file.
    public static byte[] Export(RecordingReplay replay)
    {
        using var bytes=new MemoryStream();
        foreach(var frame in replay.Frames){var chunk=Convert.FromBase64String(frame.Data);if(bytes.Length+chunk.Length>SessionRecording.MaxOutputBytes)throw new IOException("Registrazione troppo grande.");bytes.Write(chunk);}
        var input=Encoding.UTF8.GetString(bytes.ToArray());var text=new StringBuilder();int mode=0;
        foreach(char c in input)
        {
            if(mode==1){mode=c=='['?2:c is ']' or 'P' or '_' or '^'?3:0;continue;}
            if(mode==2){if(c is >= '@' and <= '~')mode=0;continue;}
            if(mode==3){if(c=='\a'||c=='\u009c')mode=0;else if(c=='\x1b')mode=4;continue;}
            if(mode==4){mode=c=='\\'?0:3;continue;}
            if(c=='\x1b'){mode=1;continue;}if(c=='\u009b'){mode=2;continue;}if(c is '\u009d' or '\u0090' or '\u009f'){mode=3;continue;}
            if(c is '\n' or '\r' or '\t'||!char.IsControl(c))text.Append(c);
        }
        return Encoding.UTF8.GetBytes(text.ToString().Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n').Replace("\n","\r\n",StringComparison.Ordinal));
    }
    public static void SaveNew(string path,byte[] text)
    {
        if(!Path.GetExtension(path).Equals(".txt",StringComparison.OrdinalIgnoreCase))throw new IOException("Scegli un file .txt.");
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(text);output.Flush(true);
    }
}

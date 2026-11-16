using Renci.SshNet.Sftp;
namespace UnscRelay.Core;

public static class FilePresentation
{
    public static string Permissions(SftpFileAttributes a) => new(new[] {
        a.IsSymbolicLink ? 'l' : a.IsDirectory ? 'd' : '-',
        a.OwnerCanRead ? 'r' : '-', a.OwnerCanWrite ? 'w' : '-',
        a.IsUIDBitSet ? (a.OwnerCanExecute ? 's' : 'S') : (a.OwnerCanExecute ? 'x' : '-'),
        a.GroupCanRead ? 'r' : '-', a.GroupCanWrite ? 'w' : '-',
        a.IsGroupIDBitSet ? (a.GroupCanExecute ? 's' : 'S') : (a.GroupCanExecute ? 'x' : '-'),
        a.OthersCanRead ? 'r' : '-', a.OthersCanWrite ? 'w' : '-',
        a.IsStickyBitSet ? (a.OthersCanExecute ? 't' : 'T') : (a.OthersCanExecute ? 'x' : '-')
    });
    public static string WindowsAttributes(FileAttributes attributes) => string.Concat(
        attributes.HasFlag(FileAttributes.ReadOnly) ? "R" : "",
        attributes.HasFlag(FileAttributes.Hidden) ? "H" : "",
        attributes.HasFlag(FileAttributes.System) ? "S" : "",
        attributes.HasFlag(FileAttributes.Archive) ? "A" : "",
        attributes.HasFlag(FileAttributes.Compressed) ? "C" : "",
        attributes.HasFlag(FileAttributes.Encrypted) ? "E" : "",
        attributes.HasFlag(FileAttributes.ReparsePoint) ? "L" : "");
}

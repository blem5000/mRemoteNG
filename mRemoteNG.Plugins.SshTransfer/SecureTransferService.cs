using System;
using System.IO;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using static System.IO.FileMode;

namespace mRemoteNG.Plugins.SshTransfer;

internal sealed class SecureTransferService : IDisposable
{
    private readonly string _host;
    private readonly string _user;
    private readonly string _password;
    private readonly int _port;

    public SecureTransferService(string host, string user, string password, int port, SshTransferProtocol protocol, string sourceFile, string destinationFile)
    {
        _host = host;
        _user = user;
        _password = password;
        _port = port;
        Protocol = protocol;
        SourceFile = sourceFile;
        DestinationFile = destinationFile;
    }

    public SshTransferProtocol Protocol { get; }

    public string SourceFile { get; }

    public string DestinationFile { get; }

    public ScpClient? ScpClient { get; private set; }

    public SftpClient? SftpClient { get; private set; }

    public SftpUploadAsyncResult? AsyncResult { get; private set; }

    public AsyncCallback? AsyncCallback { get; set; }

    public void Connect()
    {
        if (Protocol == SshTransferProtocol.Scp)
        {
            // Quote remote paths for the remote shell. The obsolete constructor defaults to
            // DoubleQuote, which does not neutralize $(...) / backticks - the same bug the main
            // application already fixed in Tools/SecureTransfer.cs.
            ScpClient = new ScpClient(_host, _port, _user, _password, RemotePathTransformation.ShellQuote);
            TrustHostKey(ScpClient);
            ScpClient.Connect();
            return;
        }

        SftpClient = new SftpClient(_host, _port, _user, _password);
        TrustHostKey(SftpClient);
        SftpClient.Connect();
    }

    /// <summary>
    /// Reject an unrecognised or changed host key. SSH.NET would otherwise accept any key,
    /// allowing a MITM to capture the credentials and the transferred file.
    /// </summary>
    private void TrustHostKey(Renci.SshNet.BaseClient client)
    {
        client.HostKeyReceived += (_, args) =>
        {
            KnownHostsStore.TrustResult result =
                KnownHostsStore.CheckOrRemember(_host, _port, args.FingerPrintSHA256 ?? string.Empty);
            args.CanTrust = result != KnownHostsStore.TrustResult.Changed;
            if (result == KnownHostsStore.TrustResult.Changed)
                throw new InvalidOperationException(
                    $"The SSH host key for {_host}:{_port} has changed. Refusing to connect.");
        };
    }

    public void Disconnect()
    {
        if (Protocol == SshTransferProtocol.Scp)
        {
            ScpClient?.Disconnect();
            return;
        }

        SftpClient?.Disconnect();
    }

    public void Upload()
    {
        if (Protocol == SshTransferProtocol.Scp)
        {
            if (ScpClient?.IsConnected != true)
            {
                throw new InvalidOperationException("SCP Not Connected!");
            }

            ScpClient.Upload(new FileInfo(SourceFile), DestinationFile);
            return;
        }

        if (SftpClient?.IsConnected != true)
        {
            throw new InvalidOperationException("SFTP Not Connected!");
        }

        AsyncResult = (SftpUploadAsyncResult)SftpClient.BeginUploadFile(new FileStream(SourceFile, Open), DestinationFile, AsyncCallback);
    }

    public void Dispose()
    {
        ScpClient?.Dispose();
        SftpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

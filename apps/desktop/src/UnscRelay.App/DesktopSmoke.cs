using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Forms.Integration;
using RemoteViewing.Vnc;
using RemoteViewing.Vnc.Server;
using UnscRelay.Core;

namespace UnscRelay.App;
public partial class MainWindow
{
    sealed class FixtureFramebuffer : IVncFramebufferSource
    {
        public VncFramebuffer Framebuffer { get; } = new("UNSC VNC TEST", 64, 32);
        public FixtureFramebuffer() { for (var y = 0; y < 32; y++) for (var x = 0; x < 64; x++) Framebuffer.SetPixel(x, y, x < 32 ? 0x224466 : 0x99bb44); }
        public VncFramebuffer Capture() => Framebuffer;
    }
    async Task CheckDesktopComponents()
    {
        var checks = new Dictionary<string, bool>(); var componentVersion = ""; string? error = null; var rdpEvents = new List<string>();
        try
        {
            vault.Unlock("Desktop smoke fixture master!");
            var fixtureProfile = new ConnectionProfile { Id = "credential-smoke", Name = "Credential fixture", Host = "localhost", Username = "fixture", Authentication = "password" };
            var fixturePayload = JsonSerializer.SerializeToElement(new { fixtureProfile.Id, fixtureProfile.Name, fixtureProfile.Host, fixtureProfile.Username, fixtureProfile.Authentication, password = "Stored smoke password!", rememberPassword = true }, ProfileStore.Json);
            SaveConnectionProfile(fixturePayload); var saved = data.Profiles.Single(p => p.Id == fixtureProfile.Id);
            checks["nativeProfileSaveEncryptsPassword"] = saved.PasswordId.Length > 0 && ConnectionPassword(saved) == "Stored smoke password!" && !File.ReadAllText(Path.Combine(dataPath, "profiles.json")).Contains("Stored smoke password!");
            checks["snapshotContainsNoSecret"] = !JsonSerializer.Serialize(Snapshot(), ProfileStore.Json).Contains("Stored smoke password!");
            var replacingPayload = JsonSerializer.SerializeToElement(new { fixtureProfile.Id, fixtureProfile.Name, fixtureProfile.Host, fixtureProfile.Username, fixtureProfile.Authentication, password = "Replacement smoke password!", rememberPassword = true }, ProfileStore.Json);
            using (var held = new FileStream(Path.Combine(dataPath, "profiles.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { SaveConnectionProfile(replacingPayload); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            checks["profileWriteFailurePreservesOriginalCredential"] = ConnectionPassword(data.Profiles.Single(p => p.Id == fixtureProfile.Id)) == "Stored smoke password!" && vault.Passwords().Count(p => p.Name == fixtureProfile.Name) == 1;
            var revocationsBefore = vault.Revocations().Count;
            var revocationKey = KeyGeneration.Generate(vault, "Revocation smoke user", "ed25519"); var revocationCa = KeyGeneration.Generate(vault, "Revocation smoke CA", "ed25519");
            var issued = vault.IssueCertificate(revocationKey.Id, revocationCa.Id, "fixture", 1); vault.RevokeCertificate(issued.Id, "Native revocation fixture");
            var revocationProfile = new ConnectionProfile { Id = "revocation-smoke", Name = "Revocation smoke", Host = "127.0.0.1", Port = 1, Username = "fixture", Authentication = "key", KeyId = issued.Id };
            try { using var unexpected = await CreateAuthenticatedSession(revocationProfile, null, 0, "revoked-smoke"); checks["nativeAuthenticationRejectsRevokedCertificateBeforeNetwork"] = false; }
            catch (InvalidOperationException ex) when (ex.Message.Contains("revocato")) { checks["nativeAuthenticationRejectsRevokedCertificateBeforeNetwork"] = true; }
            var snapshot = JsonSerializer.Serialize(Snapshot(), ProfileStore.Json); checks["snapshotExposesPublicRevocationsOnly"] = snapshot.Contains("revocations") && snapshot.Contains("Native revocation fixture") && !snapshot.Contains("PRIVATE KEY");
            var renewed = vault.IssueCertificate(issued.Id, revocationCa.Id, "fixture", 1); using (var permitted = await CreateAuthenticatedSession(revocationProfile, null, 0, "renewed-smoke")) checks["nativeAuthenticationAcceptsExplicitRenewalWithoutConnecting"] = !permitted.Connected;
            vault.Delete(renewed.Id); vault.Delete(revocationCa.Id); checks["nativeDeletionRetainsRevocationLedger"] = vault.Revocations().Count == revocationsBefore + 1;
            var krl = await OpenSshKrl.GenerateAsync(vault.Revocations()); checks["nativeKrlExportRemainsValidAfterDeletion"] = await OpenSshKrl.QueryAsync(krl, issued.Certificate!.Text);
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var rdp = new RdpView((text, connected) => { rdpEvents.Add(text); if (!connected && text.StartsWith("RDP disconnesso", StringComparison.Ordinal)) disconnected.TrySetResult(); }); ((ISupportInitialize)rdp).BeginInit();
            using (var host = new WindowsFormsHost { Child = rdp, Width = 600, Height = 400 })
            {
                DesktopLayer.Children.Add(host); host.UpdateLayout(); ((ISupportInitialize)rdp).EndInit(); rdp.CreateControl();
                componentVersion = rdp.ComponentVersion; rdp.Configure(new ConnectionProfile { Protocol = "rdp", Host = "localhost", Username = "fixture", Port = 3389, Authentication = "password" }, "NOT-A-REAL-PASSWORD", 800, 600);
                checks["rdpActiveXCreatedAndConfigured"] = componentVersion.Length > 0;
                rdp.Configure(new ConnectionProfile { Protocol = "rdp", Host = "127.0.0.1", Username = "fixture", Authentication = "password", Rdp = new() { MultiMonitor = true } }, "NOT-A-REAL-PASSWORD", 800, 600);
                checks["rdpEmbeddedMultimonitorEnabledViaWindowsInterface"] = rdp.MultiMonitorEnabled;

                rdp.Configure(new ConnectionProfile { Protocol = "rdp", Host = "127.0.0.1", Username = "fixture", Port = 49321, Authentication = "password" }, "NOT-A-REAL-PASSWORD", 800, 600);
                var settings = rdp.EndpointSettings();
                checks["rdpEmbeddedMultimonitorResetsForSingleMonitor"] = !rdp.MultiMonitorEnabled;
                checks["rdpLoopbackEndpointPreservesNlaAndCertificateVerification"] = settings.Host == "127.0.0.1" && settings.Port == 49321 && settings.Nla && settings.Authentication == 2;
                using var refusedListener = new TcpListener(IPAddress.Loopback, 0); refusedListener.Start(); var refusedPort = ((IPEndPoint)refusedListener.LocalEndpoint).Port; refusedListener.Stop();
                rdp.Connect(new ConnectionProfile { Protocol = "rdp", Host = "127.0.0.1", Username = "fixture", Port = refusedPort, Authentication = "password", ConnectTimeoutSeconds = 5 }, "NOT-A-REAL-PASSWORD", 800, 600);
                await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(12)); checks["rdpRefusedConnectionReportsStatus"] = true;
                DesktopLayer.Children.Remove(host);
            }
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var source = new FixtureFramebuffer();
            var server = new VncServerSession(); server.PasswordProvided += (_, args) => args.Accept("fixture".ToCharArray()); server.SetFramebufferSource(source);
            try
            {
                var accept = Task.Run(async () => { var socket = await listener.AcceptTcpClientAsync(); server.Connect(socket.GetStream(), new VncServerSessionOptions { AuthenticationMethod = AuthenticationMethod.Password }); return socket; });
                var id = Guid.NewGuid().ToString(); var profile = new ConnectionProfile { Id = "vnc-smoke", Name = "VNC fixture", Protocol = "vnc", Authentication = "password", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
                connectionPasswords[profile.Id] = "fixture"; desktopRects[id] = new(350, 180, 800, 400);
                await ConnectDesktop(profile, id); using var accepted = await accept.WaitAsync(TimeSpan.FromSeconds(5));
                var view = desktopSessions[id];
                for (var i = 0; i < 60; i++)
                {
                    if (view.Vnc!.Client.Framebuffer is {} fb && fb.Width == 64 && fb.GetPixels().SequenceEqual(source.Framebuffer.GetPixels())) { checks["vncAuthenticatedFramebufferReceived"] = true; break; }
                    await Task.Delay(50);
                }
                checks["vncEmbeddedHostVisible"] = view.Host.IsVisible && view.Host.Width == 800;
                desktopRects.Clear(); ApplyDesktopLayout(); checks["desktopHiddenForModalOrInactiveTab"] = !view.Host.IsVisible;
                desktopRects[id] = new(360, 190, 600, 300); ApplyDesktopLayout(); checks["desktopRepositionedAfterLayout"] = view.Host.IsVisible && view.Host.Width == 600;
                DisconnectDesktop(id); checks["desktopDisposedOnTabClose"] = desktopSessions.Count == 0 && view.Disposed;
            }
            finally { listener.Stop(); server.Close(); }
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
        finally { foreach (var id in desktopSessions.Keys.ToArray()) DisconnectDesktop(id); }
        var success = error == null && checks.Count == 18 && checks.Values.All(v => v);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "desktop-smoke-result.json"), JsonSerializer.Serialize(new { success, componentVersion, checks, rdpEvents, error }, ProfileStore.Json));
        CompleteSmoke(success);
    }
}

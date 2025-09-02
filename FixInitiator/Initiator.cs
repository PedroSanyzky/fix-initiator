using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace FixInitiator;

/// <summary>
/// Encapsula toda la inicialización de QuickFIX/n y expone un único Start().
/// No necesitas crear instancias ni pasar config desde Program.
/// </summary>
public static class Initiator
{
    private static IInitiator? _initiator;

    /// <summary>
    /// Inicia el Initiator leyendo initiator.cfg y queda corriendo hasta Ctrl+C / cierre de proceso.
    /// </summary>
    public static void Start()
    {
        if (_initiator != null)
            return; // ya iniciado

        // Lee initiator.cfg del working dir (cámbialo aquí si lo quieres en otra ruta)
        var settings = new SessionSettings("initiator.cfg");

        // App de negocio y handlers
        var app = new FixApp();

        // Store/Log (archivos locales)
        var storeFactory = new FileStoreFactory(settings);
        var logFactory = new FileLogFactory(settings);

        // Fábrica de mensajes
        var messageFactory = new DefaultMessageFactory();

        // Initiator TCP/IP
        _initiator = new SocketInitiator(app, storeFactory, settings, logFactory, messageFactory);

        // Detener ordenado ante Ctrl+C / salida del proceso
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            StopInternal();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, __) => StopInternal();

        _initiator.Start(); // <- mantiene hilos internos de QuickFIX/n
    }

    private static void StopInternal()
    {
        try
        {
            _initiator?.Stop();
        }
        catch
        { /* swallow on shutdown */
        }
    }

    /// <summary>
    /// App FIX con recepción de mensajes. Se mantiene privada al Initiator.
    /// </summary>
    private class FixApp : MessageCracker, IApplication
    {
        // Ciclo de vida
        public void OnCreate(SessionID sessionID) => Console.WriteLine($"[OnCreate] {sessionID}");

        public void OnLogon(SessionID sessionID) => Console.WriteLine($"[OnLogon] {sessionID}");

        public void OnLogout(SessionID sessionID) => Console.WriteLine($"[OnLogout] {sessionID}");

        // Admin
        public void ToAdmin(
            Message message,
            SessionID sessionID
        ) { /* opcional: headers, etc. */
        }

        public void FromAdmin(Message message, SessionID sessionID) =>
            Console.WriteLine($"[FromAdmin] {message}");

        // App (negocio)
        public void ToApp(
            Message message,
            SessionID sessionID
        ) { /* cuando tú envías */
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            Console.WriteLine($"[FromApp] {message.GetType().Name} :: {message}");
            Crack(message, sessionID); // despacha a OnMessage(...) por tipo
        }

        // Handlers típicos FIX 5.0 SP2
        public void OnMessage(QuickFix.FIX50SP2.ExecutionReport msg, SessionID s)
        {
            var clOrdId = msg.GetString(Tags.ClOrdID);
            var execType = msg.GetString(Tags.ExecType);
            var ordStatus = msg.GetString(Tags.OrdStatus);
            var lastPx = msg.GetString(Tags.LastPx);
            var lastQty = msg.GetString(Tags.LastQty);
            Console.WriteLine(
                $"[ExecutionReport] ClOrdID={clOrdId} ExecType={execType} OrdStatus={ordStatus} LastPx={lastPx} LastQty={lastQty}"
            );
        }
    }
}

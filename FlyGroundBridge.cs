// FlyGroundBridge.cs
// Requiere: Microsoft Flight Simulator SimConnect SDK y referencia a SimConnect.dll
// Compilar como x64. Ejecutar con permisos normales (no es necesario admin salvo si tu sistema lo exige).

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;

namespace FlyGroundBridge
{
    class Program
    {
        // enum para eventos mapeados
        enum EVENTS : uint
        {
            TOGGLE_PUSHBACK = 0,
            // otros eventos si quieres mapearlos
        }

        static SimConnect simconnect = null;

        static void Main(string[] args)
        {
            Console.WriteLine("FlyGroundBridge - arrancando...");

            // Inicia SimConnect en hilo aparte
            var simThread = new Thread(() =>
            {
                try
                {
                    // El primer parámetro es el nombre de la conexión
                    simconnect = new SimConnect("FlyGround Bridge", IntPtr.Zero, 0, null, 0);

                    // Mapear evento (nombre de evento usado por el simulador)
                    simconnect.MapClientEventToSimEvent(EVENTS.TOGGLE_PUSHBACK, "TOGGLE_PUSHBACK");

                    // Grupo de eventos (opcional)
                    simconnect.SetNotificationGroupPriority((uint)0, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
                    Console.WriteLine("SimConnect conectado.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR al conectar SimConnect: " + ex.Message);
                }

                // Bucle vacío para mantener la conexión abierta
                while (true)
                {
                    Thread.Sleep(1000);
                }
            });
            simThread.IsBackground = true;
            simThread.Start();

            // Arranca el servidor HTTP simple
            StartHttpServer().GetAwaiter().GetResult();
        }

        static async Task StartHttpServer()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();
            Console.WriteLine("HTTP bridge escuchando en http://localhost:5000/");

            while (true)
            {
                var ctx = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx));
            }
        }

        static void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath.ToLower();
                if (ctx.Request.HttpMethod == "POST" && path == "/pushback")
                {
                    using var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    var body = sr.ReadToEnd();
                    var doc = JsonDocument.Parse(body);
                    double heading = 0;
                    double speed = 1.0;
                    if (doc.RootElement.TryGetProperty("heading", out var h)) heading = h.GetDouble();
                    if (doc.RootElement.TryGetProperty("speed", out var s)) speed = s.GetDouble();

                    Console.WriteLine($"Pushback request recibida — heading: {heading}, speed: {speed}");

                    // Convertir heading (0..360) a valor requerido por KEY_TUG_HEADING (0..65535)
                    uint tugHeadingValue = (uint)(heading / 360.0 * 65535.0);

                    // 1) Enviar KEY_TUG_HEADING
                    try
                    {
                        // Mapear el evento de KEY_TUG_HEADING sobre la marcha (si no está mapeado)
                        simconnect.MapClientEventToSimEvent(1, "KEY_TUG_HEADING");
                        simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, 1, tugHeadingValue, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                        Console.WriteLine("KEY_TUG_HEADING enviado.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error enviando KEY_TUG_HEADING: " + ex.Message);
                    }

                    // 2) Opcional: ajustar velocidad (KEY_TUG_SPEED)
                    try
                    {
                        simconnect.MapClientEventToSimEvent(2, "KEY_TUG_SPEED");
                        uint speedValue = (uint)Math.Max(1, speed); // en ft/s según documentación
                        simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, 2, speedValue, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                        Console.WriteLine("KEY_TUG_SPEED enviado.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error enviando KEY_TUG_SPEED: " + ex.Message);
                    }

                    // 3) Finalmente toggle pushback
                    try
                    {
                        simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.TOGGLE_PUSHBACK, 1, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                        Console.WriteLine("TOGGLE_PUSHBACK enviado.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error enviando TOGGLE_PUSHBACK: " + ex.Message);
                    }

                    byte[] resp = Encoding.UTF8.GetBytes("{\"ok\":true}");
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.OutputStream.Write(resp, 0, resp.Length);
                    ctx.Response.Close();
                    return;
                }

                // ruta no encontrada
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("HandleRequest error: " + ex.Message);
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }
    }
}

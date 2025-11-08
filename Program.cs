using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using Microsoft.FlightSimulator.SimConnect;
using System.Threading;

namespace FlyGroundBridge
{
    enum EVENTS : uint
    {
        TOGGLE_PUSHBACK = 0,
        KEY_TUG_HEADING = 1,
        KEY_TUG_SPEED = 2,
        TOGGLE_REFUEL = 3,
        TOGGLE_CATERING = 4,
        TOGGLE_CLEANING = 5,
        TOGGLE_PASSENGERS = 6
    }

    class Program
    {
        static SimConnect? simconnect;

        static void Main(string[] args)
        {
            Console.WriteLine("FlyGroundBridge v0.2 — arrancando...");

            // Inicia el hilo de SimConnect
            var thread = new Thread(SimConnectThread) { IsBackground = true };
            thread.Start();

            // Inicia HTTP server
            StartHttpServer().GetAwaiter().GetResult();
        }

        static void SimConnectThread()
        {
            try
            {
                simconnect = new SimConnect("FlyGround Bridge", IntPtr.Zero, 0, null, 0);
                Console.WriteLine("SimConnect conectado.");

                // Mapear eventos
                simconnect.MapClientEventToSimEvent((uint)EVENTS.TOGGLE_PUSHBACK, "TOGGLE_PUSHBACK");
                simconnect.MapClientEventToSimEvent((uint)EVENTS.KEY_TUG_HEADING, "KEY_TUG_HEADING");
                simconnect.MapClientEventToSimEvent((uint)EVENTS.KEY_TUG_SPEED, "KEY_TUG_SPEED");
                simconnect.MapClientEventToSimEvent((uint)EVENTS.TOGGLE_REFUEL, "TOGGLE_REFUEL");
                simconnect.MapClientEventToSimEvent((uint)EVENTS.TOGGLE_CATERING, "TOGGLE_CATERING");
                simconnect.MapClientEventToSimEvent((uint)EVENTS.TOGGLE_CLEANING, "TOGGLE_CLEANING");
                simconnect.MapClientEventToSimEvent((uint)EVENTS.TOGGLE_PASSENGERS, "TOGGLE_PASSENGERS");

                simconnect.SetNotificationGroupPriority(0, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error SimConnect: " + ex.Message);
            }

            // Mantener hilo vivo
            while (true)
            {
                Thread.Sleep(1000);
            }
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
                _ = Task.Run(() => Handle(ctx));
            }
        }

        static void Handle(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath.ToLower();
                if (ctx.Request.HttpMethod == "POST" && path == "/service")
                {
                    using var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    var body = sr.ReadToEnd();
                    var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var service = root.GetProperty("service").GetString();
                    var payload = root.GetProperty("payload");

                    Console.WriteLine($"[HTTP] Servicio recibido: {service} - payload: {payload}");

                    bool ok = HandleService(service, payload);

                    var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok }));
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.OutputStream.Write(resp, 0, resp.Length);
                    ctx.Response.Close();
                    return;
                }
                else if (ctx.Request.HttpMethod == "POST" && path == "/service/cancel")
                {
                    using var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    var body = sr.ReadToEnd();
                    var doc = JsonDocument.Parse(body);
                    var service = doc.RootElement.GetProperty("service").GetString();
                    Console.WriteLine($"[HTTP] Cancel request: {service}");
                    // Implementar cancel si se desea
                    var resp = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true }));
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.OutputStream.Write(resp, 0, resp.Length);
                    ctx.Response.Close();
                    return;
                }

                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Handle error: " + ex.Message);
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }

        static bool HandleService(string? service, JsonElement payload)
        {
            if (simconnect == null)
            {
                Console.WriteLine("No hay conexión SimConnect.");
                return false;
            }

            try
            {
                switch ((service ?? "").ToLower())
                {
                    case "pushback":
                        {
                            double heading = payload.GetProperty("heading").GetDouble();
                            double speed = payload.TryGetProperty("speed", out var s) ? s.GetDouble() : 1.0;
                            Console.WriteLine($"Pushback: heading={heading} speed={speed}");
                            SendTugHeading(heading);
                            SendTugSpeed(speed);
                            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.TOGGLE_PUSHBACK, 1, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                            break;
                        }
                    case "catering":
                        {
                            Console.WriteLine("Catering toggle");
                            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.TOGGLE_CATERING, 1, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                            break;
                        }
                    case "fuel":
                        {
                            Console.WriteLine("Refuel toggle");
                            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.TOGGLE_REFUEL, 1, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                            break;
                        }
                    case "cleaning":
                        {
                            Console.WriteLine("Cleaning toggle");
                            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.TOGGLE_CLEANING, 1, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                            break;
                        }
                    case "passengers":
                        {
                            Console.WriteLine("Passengers toggle");
                            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.TOGGLE_PASSENGERS, 1, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                            break;
                        }
                    default:
                        Console.WriteLine("Servicio no reconocido: " + service);
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error HandleService: " + ex.Message);
                return false;
            }
        }

        static void SendTugHeading(double heading)
        {
            if (simconnect == null) return;
            uint val = (uint)Math.Round(heading / 360.0 * 65535.0);
            try
            {
                simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.KEY_TUG_HEADING, val, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                Console.WriteLine($"KEY_TUG_HEADING enviado: {val}");
            }
            catch (Exception ex) { Console.WriteLine("Error KEY_TUG_HEADING: " + ex.Message); }
        }

        static void SendTugSpeed(double speed)
        {
            if (simconnect == null) return;
            uint val = (uint)Math.Max(1, Math.Round(speed));
            try
            {
                simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (uint)EVENTS.KEY_TUG_SPEED, val, SimConnect.SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                Console.WriteLine($"KEY_TUG_SPEED enviado: {val}");
            }
            catch (Exception ex) { Console.WriteLine("Error KEY_TUG_SPEED: " + ex.Message); }
        }
    }
}

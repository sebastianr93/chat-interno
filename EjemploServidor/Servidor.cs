using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EjemploServidor
{
    // Declaro una subclase de EventArgs para los eventos del servidor
    public class ServidorEventArgs : EventArgs
    {
        public ServidorEventArgs(IPEndPoint ep)
        {
            EndPoint = ep;
        }

        public IPEndPoint EndPoint { get; }
    }

    // Declaro una subclase de ServidorEventArgs específicamente para el evento DatosRecibidos
    public class DatosRecibidosEventArgs : ServidorEventArgs
    {
        public DatosRecibidosEventArgs(IPEndPoint ep, string datos) : base(ep)
        {
            DatosRecibidos = datos;
        }

        public string DatosRecibidos { get; set; }
    }

    public class Servidor
    {
        // Esta estructura permite guardar la información sobre un cliente
        private struct InfoDeUnCliente
        {
            public Socket Socket; // Socket utilizado para mantener la conexión con el cliente
            public Thread Thread; // Thread utilizado para escuchar al cliente
        }

        Thread listenerThread; // Thread de escucha de nuevas conexiones
        TcpListener listener; // Este objeto nos permite escuchar las conexiones entrantes

        // En este dictionary vamos a guardar la información de todos los clientes conectados.
        // ConcurrentDictionary se puede usar desde múltiples threads sin necesidad de locks.
        // Ver: https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2
        ConcurrentDictionary<IPEndPoint, InfoDeUnCliente> clientes = new ConcurrentDictionary<IPEndPoint, InfoDeUnCliente>();

        public event EventHandler<ServidorEventArgs> NuevaConexion;
        public event EventHandler<ServidorEventArgs> ConexionTerminada;
        public event EventHandler<DatosRecibidosEventArgs> DatosRecibidos;

        public int PuertoDeEscucha { get; }

        public Servidor(int puerto)
        {
            PuertoDeEscucha = puerto;
        }

        public void Escuchar()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, PuertoDeEscucha);
                listener.Start();

                Console.WriteLine("Servidor activo en puerto: " + PuertoDeEscucha);
                MessageBox.Show("Escuchando en puerto: " + PuertoDeEscucha);

                listenerThread = new Thread(() =>
                {
                    Console.WriteLine("Esperando clientes...");
                    EsperarCliente();
                });
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fallo en Escuchar(): " + ex.Message);
            }
        }

        public void Cerrar()
        {
            // Recorro todos los clientes y voy cerrando las conexiones
            foreach (var cliente in clientes.Values)
            {
                // Cierro la conexión con el cliente
                cliente.Socket.Close();
            }
        }

        public void EnviarDatos(string datos)
        {
            // Recorro todos los clientes conectados y les envío el mensaje en el parámetro datos
            foreach (var cliente in clientes.Values)
            {
                // Envío el mensaje codificado en UTF-8 (https://es.wikipedia.org/wiki/UTF-8)
                cliente.Socket.Send(Encoding.UTF8.GetBytes(datos));
            }
        }

        private void EsperarCliente()
        {
            while (true)
            {
                var socket = listener.AcceptSocket();
                var endPoint = socket.RemoteEndPoint as IPEndPoint;

                // Prepara la información del cliente
                var infoCliente = new InfoDeUnCliente
                {
                    Socket = socket
                };

                // Guarda en el diccionario antes de lanzar el thread
                clientes[endPoint] = infoCliente;

                // Dispara el evento de nueva conexión
                NuevaConexion?.Invoke(this, new ServidorEventArgs(endPoint));

                var thread = new Thread(() => LeerSocket(endPoint));
                infoCliente.Thread = thread;
                clientes[endPoint] = infoCliente;
                thread.IsBackground = true;
                thread.Start();
                // Notifico a todos los clientes conectados sobre la nueva conexión
                NotificarClientesConectados();
            }
        }


        private void LeerSocket(IPEndPoint endPoint)
        {
            var buffer = new byte[100]; // Array a utilizar para recibir los datos que llegan
            var cliente = clientes[endPoint]; // Información del cliente que se va a escuchar
            while (cliente.Socket.Connected)
            {
                try
                {
                    // Me quedo esperando a que llegue un mensaje desde el cliente
                    int cantidadRecibida = cliente.Socket.Receive(buffer, buffer.Length, SocketFlags.None);

                    // Me fijo cuántos bytes recibí. Si no recibí nada, entonces se desconectó el cliente
                    if (cantidadRecibida > 0)
                    {
                        // Decodifico el mensaje recibido usando UTF-8 (https://es.wikipedia.org/wiki/UTF-8)
                        var datosRecibidos = Encoding.UTF8.GetString(buffer, 0, cantidadRecibida);

                        // Disparo el evento de la recepción del mensaje
                        DatosRecibidos?.Invoke(this, new DatosRecibidosEventArgs(endPoint, datosRecibidos));
                    }
                    else
                    {
                        // Disparo el evento de la finalización de la conexión
                        ConexionTerminada?.Invoke(this, new ServidorEventArgs(endPoint));
                        break;
                    }
                }
                catch
                {
                    if (!cliente.Socket.Connected)
                    {
                        // Disparo el evento de la finalización de la conexión
                        ConexionTerminada?.Invoke(this, new ServidorEventArgs(endPoint));
                        break;
                    }
                }
            }
            // Elimino el cliente del dictionary que guarda la información de los clientes
            clientes.TryRemove(endPoint, out cliente);
            NotificarClientesConectados();
        }

        // Este método se utiliza para notificar a todos los clientes conectados
        private void NotificarClientesConectados()
        {
            string lista = string.Join(",", clientes.Keys.Select(ep => ep.ToString()));
            string mensaje = $"CLIENTES:{lista}";

            foreach (var cliente in clientes.Values)
            {
                cliente.Socket.Send(Encoding.UTF8.GetBytes(mensaje));
            }
        }

        // Este método se utiliza para enviar un mensaje a un cliente específico
        public void EnviarDatosA(IPEndPoint destino, string mensaje)
        {
            if (clientes.TryGetValue(destino, out var cliente))
            {
                cliente.Socket.Send(Encoding.UTF8.GetBytes(mensaje));
            }
        }

        public List<IPEndPoint> ObtenerClientesConectados()
        {
            return clientes.Keys.ToList();
        }

    }
}

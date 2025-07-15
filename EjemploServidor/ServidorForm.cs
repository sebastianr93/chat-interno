using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EjemploServidor
{
    public partial class ServidorForm : Form
    {
        Servidor servidor;

        public ServidorForm()
        {
            InitializeComponent();
        }

        private void Log(string texto)
        {
            // Invoke nos permite ejecutar un delegado en el tread de la UI. 
            // El problema radica en que no es seguro interactuar con los controles
            // de Windows Forms desde múltiples threads. Y en este ejemplo, el 
            // método Log se está llamando desde eventos que se disparan desde
            // threads creados en el objeto Servidor.
            // Ver: https://docs.microsoft.com/en-us/dotnet/framework/winforms/controls/how-to-make-thread-safe-calls-to-windows-forms-controls
            Invoke((Action)delegate
            {
                txtLog.AppendText($"{DateTime.Now.ToShortTimeString()} - {texto}\r\n");
                txtLog.AppendText("\n");
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Inicializo el servidor estableciendo el puerto donde escuchar
            servidor = new Servidor(8050);

            // Me suscribo a los eventos
            servidor.NuevaConexion += Servidor_NuevaConexion;
            servidor.ConexionTerminada += Servidor_ConexionTerminada;
            servidor.DatosRecibidos += Servidor_DatosRecibidos;

            // Comienzo la escucha
            servidor.Escuchar();
        }

        // Método para actualizar la lista de clientes conectados
        private void ActualizarListaClientes()
        {
            Invoke((Action)(() =>
            {
                cmbClientes.Items.Clear();
                cmbClientes.Items.Add("Todos");
                foreach (var ep in servidor.ObtenerClientesConectados())
                {
                    cmbClientes.Items.Add(ep.ToString());
                }
                cmbClientes.SelectedIndex = 0;
            }));
        }

        // Método para mostrar mensajes en el log del servidor
        private void MostrarMensajeServidor(string mensaje)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() =>
                {
                    txtLog.AppendText(mensaje + Environment.NewLine);
                }));
            }
            else
            {
                txtLog.AppendText(mensaje + Environment.NewLine);
            }
        }


        private void Servidor_NuevaConexion(object sender, ServidorEventArgs e)
        {
            //  Muestro quién se conectó
            Log($"Se ha conectado un nuevo cliente desde la IP = {e.EndPoint.Address}, Puerto = {e.EndPoint.Port}");
            ActualizarListaClientes();
        }

        private void Servidor_ConexionTerminada(object sender, ServidorEventArgs e)
        {
            // Muestro con quién se terminó la conexión
            Log($"Se ha desconectado el cliente de la IP = {e.EndPoint.Address}, Puerto = {e.EndPoint.Port}");
            ActualizarListaClientes();
        }

        private void Servidor_DatosRecibidos(object sender, DatosRecibidosEventArgs e)
        {
            string datos = e.DatosRecibidos;

            if (datos.StartsWith("TO:"))
            {
                int sep = datos.IndexOf('|');
                string destino = datos.Substring(3, sep - 3); // Ej: ALL o IP:PUERTO
                string contenido = datos.Substring(sep + 1);

                if (destino == "ALL")
                {
                    // Mostrar en el chat del servidor (suponiendo que tienes un método MostrarMensajeServidor)
                    MostrarMensajeServidor($"Desde {e.EndPoint}: {contenido}");

                    // Reenviar mensaje a todos los clientes conectados
                    servidor.EnviarDatos($"Desde {e.EndPoint}: {contenido}");
                }
                else
                {
                    var partes = destino.Split(':');
                    if (partes.Length == 2 &&
                        IPAddress.TryParse(partes[0], out var ip) &&
                        int.TryParse(partes[1], out var port))
                    {
                        var ep = new IPEndPoint(ip, port);

                        // Mostrar en el servidor mensaje privado (opcional)
                        MostrarMensajeServidor($"(Privado a {ep}): Desde {e.EndPoint}: {contenido}");

                        servidor.EnviarDatosA(ep, $"Desde {e.EndPoint}: {contenido}");
                    }
                }
            }
        }



        private void btnEnviarMensaje_Click(object sender, EventArgs e)
        {
            string destino = cmbClientes.SelectedItem.ToString();
            string mensaje = txtMensaje.Text;

            if (destino == "Todos")
            {
                servidor.EnviarDatos($"Servidor: {mensaje}");
            }
            else
            {
                var partes = destino.Split(':');
                if (partes.Length == 2 &&
                    IPAddress.TryParse(partes[0], out var ip) &&
                    int.TryParse(partes[1], out var port))
                {
                    var ep = new IPEndPoint(ip, port);
                    servidor.EnviarDatosA(ep, $"Servidor: {mensaje}");
                }
            }

            Log($"Servidor a {destino}: {mensaje}");
        }

    }
}

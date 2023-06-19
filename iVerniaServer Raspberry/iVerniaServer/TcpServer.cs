using System;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace iVerniaServer
{
	public class TcpServer
	{

		// Declaramos todas las constantes que utilizamos en el protocolo de comunicaciones con el cliente android.
		const char ETX = (char)0x03;
		const char STX = (char)0x02;

		const char CMD_MODO = (char)0x10;
		const char CMD_MOTOR = (char)0x11;
		const char CMD_VENTANA = (char)0x12;
		const char CMD_CONFIGURACION = (char)0x13;

		byte CMD_ESTADO = 0x14;


		// Variables que utilizaremos para comunicación socket
		public AsyncCallback pfnWorkerCallBack;
		private Socket m_mainSocket;
		private Socket m_clientSocket;

		Configuracion conf;
		string respuesta = "";

		// En el constructor de la clase creamos el socket servidor e iniciamos la escucha para aceptar la conexión cliente.
		public TcpServer(Configuracion conf, int puerto)
		{
			this.conf = conf;

			m_mainSocket = new Socket(AddressFamily.InterNetwork,
									  SocketType.Stream,
									  ProtocolType.Tcp);
			IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, puerto);
			m_mainSocket.Bind(ipLocal);
			m_mainSocket.Listen(10);
			m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
		}

		// Esta función se llama cuando se conecta un cliente. 
		// Si existe una conexión anterior se cierra ya que este servidor esta programado para que acepte una única conexión.
		// Una vez aceptada la conexión se envia el comando de estado al cliente.
		public void OnClientConnect(IAsyncResult asyn)
		{
			try
			{
				if (m_clientSocket != null)
				{
					m_clientSocket.Close();
					m_clientSocket.Dispose();
				}

				m_clientSocket = m_mainSocket.EndAccept(asyn);
				WaitForData(m_clientSocket);
				EnviaEstado();

				m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
			}
			catch (ObjectDisposedException)
			{
				System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket has been closed\n");
			}
			catch (SocketException se)
			{

			}

		}

		// Estructura que contiene el buffer que utilizaremos para almacenar los datos enviados desde el cliente
		public class SocketPacket
		{
			public System.Net.Sockets.Socket m_currentSocket;
			public byte[] dataBuffer = new byte[256];
		}

		// Función que inicia la escucha de los datos procedentes del cliente
		// Se asigna la función "Callback" que se llamará cuando se reciban los datos (OnDataReceived)
		public void WaitForData(System.Net.Sockets.Socket soc)
		{
			try
			{
				if (pfnWorkerCallBack == null)
				{
					pfnWorkerCallBack = new AsyncCallback(OnDataReceived);
				}
				SocketPacket theSocPkt = new SocketPacket();
				theSocPkt.m_currentSocket = soc;
				soc.BeginReceive(theSocPkt.dataBuffer, 0,
								   theSocPkt.dataBuffer.Length,
								   SocketFlags.None,
								   pfnWorkerCallBack,
								   theSocPkt);
			}
			catch (SocketException se)
			{

			}

		}

		// Función que se llamará cuando el servidor detecte escritura de datos por parte del cliente.
		// Los datos recibidos se pasan a la función "OnData" que se encargará de identificar y procesar los comandos.
		public void OnDataReceived(IAsyncResult asyn)
		{
			try
			{
				SocketPacket socketData = (SocketPacket)asyn.AsyncState;

				int iRx = 0;
				iRx = socketData.m_currentSocket.EndReceive(asyn);
				char[] chars = new char[iRx];
				System.Text.Decoder d = System.Text.Encoding.Default.GetDecoder();
				int charLen = d.GetChars(socketData.dataBuffer,
										 0, iRx, chars, 0);
				System.String szData = new System.String(chars);
				OnData(szData);

				WaitForData(socketData.m_currentSocket);
			}
			catch (ObjectDisposedException)
			{
				System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
			}
			catch (SocketException se)
			{

			}
		}

		// Esta función descompone las tramas recibidas e identifica de que comandos se trata.
		// Actualiza las variables de estado y le devuelve al cliente el comando de estado para qu este pueda actualizar la interfaz.
		public void OnData(string cadena)
		{
			int valor;
			String[] temp;

			for (int i = 0; i < cadena.Length; i++)
			{
				if (cadena[i] == STX)
				{
					respuesta = "";
				}
				else
					if (cadena[i] == ETX)
				{
					if (respuesta.Length > 0)
					{
						switch (respuesta[0])
						{
							case CMD_MODO:
								valor = int.Parse(respuesta.Substring(1, 1));
								conf.modo = valor;

								Console.WriteLine("Modo: " + valor);

								EnviaEstado();
								break;

							case CMD_MOTOR:

								Console.WriteLine("Orden Motor: " + respuesta);

								if (conf.modo == 1)
								{
									valor = int.Parse(respuesta.Substring(1, 1));
									conf.regar = valor == 1;

									EnviaEstado();
								}
								break;

							case CMD_VENTANA:

								Console.WriteLine("Orden Ventana: " + respuesta);

								if (conf.modo == 1)
								{
									valor = int.Parse(respuesta.Substring(1, 1));
									conf.abrir_ventana = valor == 1;

									EnviaEstado();
								}
								break;

							case CMD_CONFIGURACION:

								respuesta = respuesta.Substring(1, respuesta.Length - 1);

								Console.WriteLine("Configuración Recibida: " + respuesta);

								temp = respuesta.Split(',');

								conf.hum_suelo_automatico = int.Parse(temp[0]);
								conf.usar_sensor_luz_automatico = temp[1] == "1";
								conf.tinterior_automatico = int.Parse(temp[2]);

								conf.tiempo_maximo_riego = int.Parse(temp[3]);
								conf.pos_ventana_abierta = int.Parse(temp[4]);
								conf.pos_ventana_cerrada = int.Parse(temp[5]);

								GuardaConfiguracion(respuesta);

								break;

							default:

								break;
						}
					}
				}
				else
				{
					respuesta += cadena[i];
				}

			}
		}

		// Esta función guarda la configuración enviada por el cliente en el archivo "iVernia.conf".
		private void GuardaConfiguracion(string cadena)
		{
			try
			{
				StreamWriter writer = new StreamWriter("/home/pi/iVernia/iVernia.conf");
				writer.WriteLine(cadena);
				writer.Close();
				writer.Dispose();
				writer = null;
			}
			catch
			{ }
		}

		// Función que genera el comando de estado y llama a la función EnviaBytes para enviarlo al cliente.
		public void EnviaEstado()
		{
			string estado;

			estado = String.Format("{0},{1},{2},{3},{4},{5},{6},{7}", conf.modo, conf.temp_exterior, conf.temp_interior, conf.hum_suelo, conf.hum_ambiente, conf.sensor_luz_activo ? 1 : 0, conf.ventana_abierta ? 1 : 0, conf.regando ? 1 : 0);
			Console.WriteLine(estado);

			byte[] bytes = new byte[estado.Length + 3];

			bytes[0] = (byte)STX;
			bytes[1] = (byte)CMD_ESTADO;

			for (int i = 0; i < estado.Length; i++)
			{
				bytes[i + 2] = (byte)estado[i];
			}

			bytes[estado.Length + 2] = (byte)ETX;
			EnviaBytes(bytes);
		}

		// Función que envia los datos al cliente.
		void EnviaBytes(byte[] bytes)
		{
			try
			{
				if (m_clientSocket != null)
				{
					if (m_clientSocket.Connected)
						m_clientSocket.Send(bytes);
					else
					{
						m_clientSocket.Close();
						m_clientSocket.Dispose();

						m_clientSocket = null;
					}
				}
			}
			catch (SocketException se)
			{

			}
		}
	}
}

using System;
using System.Threading;

namespace iVerniaServer
{
	class MainClass
	{
		// Función principal de la aplicación.
		// Se crean instanancias de las clases Configuración, TcpServer y ClienteModbus.
		public static void Main(string[] args)
		{
			// En la clase configuración se guardan los valores actuales de todos los parámetros.
			// Contiene el proceso encargado de decidir cuando se inicia/para el riego y se abre/cierra la ventana en el modo automático.
			Configuracion configuracion = new Configuracion();

			// Clase que contiene el servidor TCP encargado de la comunicación con el cliente android
			TcpServer tcpServer = new TcpServer(configuracion, 2390);

			// Clase encargada de la comunicación con el dispositivo arduino. Utiliza la librería EasyModbus para ese fin.
			// Recibe los datos de los sensores y envia ordenes para manejar el riego y la ventana. 
			ClienteModbus clienteModbus = new ClienteModbus(configuracion, tcpServer);


		}
	}
}

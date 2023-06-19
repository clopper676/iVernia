using System;
using System.Threading;

namespace iVerniaServer
{
	public class ClienteModbus
	{
		public EasyModbus.ModbusClient modbusClient = null;
		Configuracion conf;
		TcpServer tcpServer;

		// Constructor de la clase ClienteModbus donde creamos el objeto modbusClient de la librería EasyModbus.
		public ClienteModbus(Configuracion conf, TcpServer tcpServer)
		{
			this.conf = conf;
			this.tcpServer = tcpServer;

			// Se le pasa como parámetros la ip del módulo arduino y el puerto 502 ( por defecto del protocolo Modbus TCP )
			modbusClient = new EasyModbus.ModbusClient("192.168.1.127", 502);

			// A continuación se inicia el proceso de comunicación con el dispositivo arduino mediante las lecturas/escrituras modbus.
			IniciaConexionModbus();
		}

		public void IniciaConexionModbus()
		{
			int cont_fallo_conexion = 0;

			// Bucle que se repite continuamente realizando las lecturas de lo sensores de arduino 
			// y las escrituras para activar/desactivar los actuadores cuando sea necesario
			while (true)
			{
				try
				{
					// Si se producen mas de dos fallos continuos de comunicación se inicializan los valores de estado a valores nulos 
					// y se envia este nuevo estado al cliente android.
					if (cont_fallo_conexion > 2)
					{
						conf.InicializaValores();
						tcpServer.EnviaEstado();
					}


					try
					{
						if (modbusClient.Connected)
							modbusClient.Disconnect();
					}
					catch { }



					// Si el estado actual de riego y ventana son diferentes del nuevo estado se envian los nuevos valores al arduino.
					if ((conf.regando != conf.regar) ||	(conf.ventana_abierta != conf.abrir_ventana))
					{
						Console.WriteLine(" --- Escritura --- ");

						modbusClient.Connect();

						if (modbusClient.Connected)
						{
							cont_fallo_conexion = 0;

							int[] values = new int[2];

							// Se llama a la función WriteMultipleRegisters para enviar al arduino los nuevos valores de regar y estado de la ventana
							values[0] = conf.regar ? 1 : 0;
							values[1] = conf.abrir_ventana ? conf.pos_ventana_abierta : conf.pos_ventana_cerrada;
							modbusClient.WriteMultipleRegisters(conf.BOMBA_HREG, values);

							try
							{
								modbusClient.Disconnect();
							}
							catch { }
						}
						else
							cont_fallo_conexion ++; // Si no es posible conectar se incrementa el contador de fallos
					}

					Thread.Sleep(300);

					modbusClient.Connect();

					if (modbusClient.Connected)
					{
						cont_fallo_conexion = 0;
						int[] valores_leidos = modbusClient.ReadHoldingRegisters(0, 7);

						if (valores_leidos[conf.TEMP1_HREG]!=0)
						{
							// Se realiza la petición al arduino de los valores de todos los sensores y actuadores
							conf.temp_exterior = valores_leidos[conf.TEMP1_HREG];
							conf.temp_interior = valores_leidos[conf.TEMP2_HREG];
							conf.setHumedadSuelo(valores_leidos[conf.HUM_SUELO_HREG]);
							conf.hum_ambiente = valores_leidos[conf.HUM_AMBIENTE_HREG];
							conf.sensor_luz_activo = valores_leidos[conf.LUZ_HREG] == 1;
							conf.regando = valores_leidos[conf.BOMBA_HREG] == 1;
							conf.ventana_abierta = valores_leidos[conf.VENTANA_HREG] == conf.pos_ventana_abierta;

							// Se escriben por pantalla los valores. Para depuración.
							Console.WriteLine(conf.temp_exterior);
							Console.WriteLine(conf.temp_interior);
							Console.WriteLine(conf.hum_suelo);
							Console.WriteLine(conf.hum_ambiente);
							Console.WriteLine(conf.sensor_luz_activo);
							Console.WriteLine(conf.regando);
							Console.WriteLine(conf.ventana_abierta);

							tcpServer.EnviaEstado();

							try
							{
								modbusClient.Disconnect();
							}
							catch { }
							}
						}
						else
							cont_fallo_conexion++; // Si no es posible conectar con arduino para realizar la lectura se incrementa el contador de fallos


					Thread.Sleep(500);  // Se espera 500 ms y se inicia de nuevo el bucle.
				}
				catch (Exception e)
				{
					cont_fallo_conexion++;
				}
			}
		}
	}
}

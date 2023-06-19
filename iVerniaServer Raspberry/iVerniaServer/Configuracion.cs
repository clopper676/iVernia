using System;
using System.Windows;
using System.Threading;
using System.IO;

namespace iVerniaServer
{

	public class Configuracion
	{
		// Parámetros que indica el modo actual de trabajo. 0 -> automático, 1 -> manual
		public int modo = 0;

		// Declaramos los registros modbus que utilizaremos para la comunicación con arduino.
		public int TEMP1_HREG = 0;
		public int TEMP2_HREG = 1;
		public int HUM_SUELO_HREG = 2;
		public int HUM_AMBIENTE_HREG = 3;
		public int LUZ_HREG = 4;
		public int BOMBA_HREG = 5;
		public int VENTANA_HREG = 6;

		// Variables donde guardamos el valor actual de los sensores y actuadores
		public int temp_exterior = -1;	
		public int temp_interior = -1;
		public int hum_suelo = -1;
		public int hum_ambiente = -1;
		public bool sensor_luz_activo = false;
		public bool ventana_abierta = false;
		public bool regando = false;
		public bool abrir_ventana = false;
		public bool regar = false;

		// Parámetros para ajustar la posición de la ventana. Abierta y cerrada.
		public int pos_ventana_abierta = 180;
		public int pos_ventana_cerrada = 0;

		//Condiciones para el modo automático
		public int tinterior_automatico = -1;
		public int hum_suelo_automatico = -1;
		public bool usar_sensor_luz_automatico = false;
		public int tiempo_maximo_riego = 30; // segundos

		// Valores que utilizamos para convertir el valor del sensor de humedad en %
		const int max_valor_humedad = 600;
		const int min_valor_humedad = 0;

		// En el constructor de la clase cargamos la configuración e iniciamos el proceso de control automático de riego
		public Configuracion()
		{
			CargaConfiguracion();
			IniciaProcesoAutomatico();
		}

		// Función que carga la configuración almacenada en el archivo iVernia.conf
		private void CargaConfiguracion()
		{
			string[] temp;
			String cadena;
			
			try
			{
				StreamReader reader = new StreamReader("/home/pi/iVernia/iVernia.conf");
				cadena = reader.ReadLine();

				reader.Close();
				reader.Dispose();
				reader = null;

				temp = cadena.Split(',');

				hum_suelo_automatico = int.Parse(temp[0]);
				usar_sensor_luz_automatico = temp[1] == "1";
				tinterior_automatico = int.Parse(temp[2]);

				tiempo_maximo_riego = int.Parse(temp[3]);
				pos_ventana_abierta = int.Parse(temp[4]);
				pos_ventana_cerrada = int.Parse(temp[5]);

			}
			catch { }
		}

		// Función que almacena el valor de la humedad del suelo en la variable "hum_suelo". 
		// Previamente pasa el valor obtenido del sensor a % de humedad. 
		public void setHumedadSuelo(int valor)
		{
			this.hum_suelo = ((int)(max_valor_humedad - valor) * 100)/max_valor_humedad;			
		}

		// Función que inicializa las variables de estado a valores nulos.
		// Se llama a esta función tras varios intentos fallidos de comunicar con el arduino.
		// De esta forma el cliente android podrá indicar en la interfaz el fallo de comunicación con el arduino.
		public void InicializaValores()
		{
			temp_exterior = -1; 
			temp_interior = -1;
			hum_suelo = -1;
			hum_ambiente = -1; 
			sensor_luz_activo = false;
			regando = false;
			ventana_abierta = false;
		}

		// Función que arran el proceso encargado de las decisiones en el modo automatico.
		private void IniciaProcesoAutomatico()
		{
			ThreadAutomatico thread = new ThreadAutomatico(this);
			Thread tComando = new Thread(new ThreadStart(thread.Accion));
			tComando.IsBackground = true;
			tComando.Start();
		}

		class ThreadAutomatico
		{
			Configuracion configuracion;
			DateTime instanteInicioRiego;

			public ThreadAutomatico(Configuracion configuracion)
			{
				this.configuracion = configuracion;
			}

			public void Accion()
			{
				while (true)
				{
					try
					{
						if (configuracion.modo == 0) // Si estamos en modo automático entramos a comprobar las variables.
						{

							// Si esta regando y ha pasado el maximo tiempo de riego permitido se para el riego y se pasa a modo manual
							if ((configuracion.regar && ((DateTime.Now - instanteInicioRiego).TotalSeconds > configuracion.tiempo_maximo_riego)))
							{
								configuracion.regar = false; 
								configuracion.modo = 1;
							}
							else
							{
								// Si no se está regando y la humedad del suelo es menor que el valor de referencia configurado pasamos a valorar el resto de variables
								if (configuracion.hum_suelo < configuracion.hum_suelo_automatico)
								{
									// Si no tenemos en cuenta el sensor de luz o si si lo utilizamos y esta activo entonces pasamos a comprobar si no estamos regando todavia
									if ((!configuracion.usar_sensor_luz_automatico) || (configuracion.usar_sensor_luz_automatico && configuracion.sensor_luz_activo))
									{
										// Si todavia no estamos regando, iniciamos el riego y guardamoms el instante de inicio del mismo.
										if (!configuracion.regar)
										{
											configuracion.regar = true;
											instanteInicioRiego = DateTime.Now;
										}
									}
									else
										configuracion.regar = false; // Si no se cumple la condición de luz paramos el riego 
								}
								else
									configuracion.regar = false; // Si no se cumple la condición de humedad paramos el riego.


								// Si la temperatura interior es mayor al valor de referencia configurado pasamos a comprobar la temperatura exterior
								if (configuracion.temp_interior > configuracion.tinterior_automatico)
								{
									// Si la temperatura exterior es menor que la interior entonces abrimos la ventana. Por el contrario la cerramos.
									if (configuracion.temp_exterior < configuracion.temp_interior)
										configuracion.abrir_ventana = true;
									else
										configuracion.abrir_ventana = false;
								}
								else
									configuracion.abrir_ventana = false; // Si la temperatura interior es menor que la referencia configurada la ventana permanece cerrada.

							}

						}
						else
						{
							instanteInicioRiego = DateTime.Now;
						}


						Thread.Sleep(1000);


					}
					catch (Exception e)
					{ }
					
				}
			}
		}
	}
}

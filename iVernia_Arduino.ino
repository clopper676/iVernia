// Se cargan las librerias que se van a utilizar:

// Libreria utilizada para gestionar la comunicacion IP mediante WIFI.
#include <ESP8266WiFi.h>

// Libreria que permite controlar los servos de forma sencilla. Se utilizara para la apertura/cierre de la ventana. 
#include <Servo.h>

// Librerias utilizadas para la lectura de temperatura de los sensores DS18B20.
#include <OneWire.h>
#include <DallasTemperature.h>

// Librerias que proporcionan la interfaz de comunicaciones MODBUS para comunicar con el servidor.
#include <Modbus.h>
#include <ModbusIP_ESP8266.h>

// Libreria para el modulo de temperatura y humedad DHT-11
#include <DHT.h>
#include <DHT_U.h>

// Se define la configuracion IP. En este caso utilizaremos una configuracion estatica.
byte ip[]      = { 192, 168, 0, 127 };
byte gateway[] = { 192, 168, 0, 1 };
byte subnet[]  = { 255, 255, 255, 0 };

// Se declara la variable que permitira gestionar la comunicacion MODBUS.
ModbusIP mb;

// Se declara la variable que se usará para la apertura/cierre de la ventana.
Servo servo_ventana;

// Se definen los pines de E/S que se usaran para cada sensor.
#define PIN_VENTANA 16
#define PIN_BOMBA 5
#define PIN_HUMEDAD 0
#define PIN_LUZ 4
#define ONE_WIRE_BUS 12
#define DHTTYPE DHT11
#define DHTPIN 13

// Se activan los dispositivos
OneWire oneWire(ONE_WIRE_BUS);
DallasTemperature DS18B20(&oneWire);
DHT dht(DHTPIN, DHTTYPE);

// Se definen las posiciones de los registros Modbus que se usaran para cada sensor/actuador.
const int TEMP1_HREG = 0;
const int TEMP2_HREG = 1;
const int HUM_TI_HREG = 2;
const int HUM_AM_HREG = 3;
const int LUZ_HREG = 4;
const int BOMBA_HREG = 5;
const int VENTANA_HREG = 6;

// Se definen las variables que se usaran para almacenar el valor de los distintos sensores.
float tempC1 = 0;
float tempC2 = 0;
int humedad1 = 0;
float humedad2 = 0;
int nivel_luz = 0;

// Se declara un flag donde se guarda el estado de la bomba, si esta funcionando o en reposo.
boolean bombaactiva = false;

// Funciones para realizar las lecturas de temperaturas
void getTemperature1() { //temperatura exterior
  DS18B20.requestTemperatures();
  tempC1 = DS18B20.getTempCByIndex(0);
  delay(100);
 }

void getTemperature2() { //temperatura ambiente
  if (isnan(dht.readTemperature())){
  }
  else
  {
    if (dht.readTemperature() != -1) {
    tempC2 = dht.readTemperature();
    }
  }
  delay(100);
 }

// La funcion "setup" junto con la funcion "loop" son las 2 funciones obligatorias en la estructura de un programa arduino. 
void setup()
{
  // Se configura la interfaz serie que se usara para depurar el programa.
  Serial.begin(9600);

  // Se configuran los parametros de la conexion wifi.
  WiFi.config(ip, gateway, subnet);
  mb.config("iVernia", "iVernia2017");

  // Se espera hasta que la conexion wifi esta establecida.
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("");
  Serial.println("WiFi conectada");
  Serial.println("Dirección IP: ");
  Serial.println(WiFi.localIP());

  delay(10);

  // Se definen en la interfaz MODBUS los registros que seran accesibles desde el servidor.
  mb.addHreg(TEMP1_HREG);
  mb.addHreg(TEMP2_HREG);
  mb.addHreg(HUM_TI_HREG);
  mb.addHreg(HUM_AM_HREG);
  mb.addHreg(LUZ_HREG);
  mb.addHreg(BOMBA_HREG);
  mb.addHreg(VENTANA_HREG);

  // Se inician los objetos que se usaran para la lectura de temperatura y humedad de los sensores DS18B20 y DHT-11.
  DS18B20.begin();
  dht.begin();

  // Se inicia el objeto que se usara para la apertura de la ventana.
  servo_ventana.attach(PIN_VENTANA);

  // Se define el tipo de E/S que se usara en cada uno de los sensores. Si es de entrada o salida.
  pinMode(PIN_BOMBA, OUTPUT);
  digitalWrite(PIN_BOMBA, LOW);
  pinMode(PIN_HUMEDAD, INPUT);
  pinMode(DHTPIN, INPUT);
  pinMode(PIN_LUZ, INPUT);
 
  delay(100);
}


// La funcion "loop" se ejecutara en bucle durante el tiempo que esta activo el sistema.
void loop()
{
  
  mb.task();

  // Lee el estado del registro MODBUS asociado a la bomba. Si se obtiene el valor 1 se activa la bomba, en caso contrario se detiene la bomba.
  Serial.print("Riego: ");
  if (mb.Hreg(BOMBA_HREG) == 1){
    digitalWrite(PIN_BOMBA, HIGH);
    Serial.println("Activado");
  }
  else {
    digitalWrite(PIN_BOMBA, LOW);
    Serial.println("Desactivado");
  }

  // Se posiciona el servo de la ventana en la posicion en grados obtenida al leer el registro MODBUS correspondiente.

  if ((mb.Hreg(VENTANA_HREG) >= 0) && (mb.Hreg(VENTANA_HREG) <= 180))
  {
    servo_ventana.write(mb.Hreg(VENTANA_HREG));
    delay(15);
  }

  // Se realizan las lecturas de temperatura y se actualizan los registros MODBUS correspondientes.

  getTemperature1();

  Serial.print("Temperatura exterior: ");
  Serial.println(tempC1);

  mb.Hreg(TEMP1_HREG, tempC1 * 100);

  getTemperature2();

  Serial.print("Temperatura ambiente: ");
  Serial.println(tempC2);

  mb.Hreg(TEMP2_HREG, tempC2 * 100);


  // Se realizan las lecturas de humedad de la tierra y ambiente y se almacenan los valores en sus registros MODBUS.

  humedad1 = analogRead(PIN_HUMEDAD);
  Serial.print("Humedad tierra: ");
  Serial.println(humedad1);
  mb.Hreg(HUM_TI_HREG, humedad1);

  if (isnan(dht.readHumidity())){
  }
  else
  {
    if (dht.readHumidity() != -1){
    humedad2 = dht.readHumidity();
    }
  }
  Serial.print("Humedad ambiente: ");
  Serial.print(humedad2);
  Serial.println("%");

  mb.Hreg(HUM_AM_HREG, humedad2);

  // Lee el estado del sensor de luz con la funcion "digitalRead" y se almacena su estado en el correspondiente registro MODBUS.

  nivel_luz = digitalRead(PIN_LUZ);
  Serial.print("Intensidad de luz: ");

  if (nivel_luz == HIGH)
    Serial.println("Alto");
  else
    Serial.println("Bajo");

  mb.Hreg(LUZ_HREG, nivel_luz);

  delay(50);
}




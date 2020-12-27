# Сети 

1. Первая программа даёт посмотреть какие IP заняты в локальной сети.

  Первым дело узнаем IP нашего сетевого интерфейса, в какой мы вообще сети :
      import socket
      def getMyIp():
          s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) #Создаем сокет (UDP)
          s.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1) # Настраиваем сокет на BROADCAST вещание.
          s.connect(('<broadcast>', 0))
          return s.getsockname()[0]
  
> socket.AF_INET — для сокета используем IPv4 .  
> socket.SOCK_DGRAM — тип сокета. Датаграммный сокет (UDP) .  
> getsockname() — Вернет сокету его собственный адрес.  
  Далее на надо узнать платформу.
  Введем диапазон адресов для сканирования :

> start_point = int(input("Enter the Starting Number: "))  
> end_point = int(input("Enter the Last Number: "))

  Ну теперь напишем саму функцию сканирования.  
> os.popen(comm) — Класс Popen Python выполняет дочернюю программу в новом процессе.  
> data = response.readlines() — читаем ответ от утилиты.  
Дальше просто парсим ответ, если есть TTL в строке значит пинг успешен.
Еще добавим многопоточность что бы быстрей пинговать. 

2. Вторая программа даёт посмотреть какие IP заняты в локальной сети и какой у вас MAC адрес. 

Продолжаем улучшать сканер локальной сети. Добавим отображение MAC адреса.

> Что такое MAC?   
  Допустим у нас есть устройство А которое хочет отправит данные устройству В. Для этого на нужно IP адрес устройства и обязательно его физический адрес.  

> MAC-адрес (Media Access Control — управление доступом к среде, также Hardware Address, также физический адрес) — уникальный идентификатор, присваиваемый каждой единице активного оборудования или некоторым их интерфейсам в компьютерных сетях Ethernet.  

Что бы узнать адрес есть протокол ARP . То есть устройство A кричит в сеть всем: устройство В с IP 10.10.10.10 какой у тебя MAC?  

> ARP (.Address Resolution Protocol — протокол определения адреса) — протокол в компьютерных сетях, предназначенный для определения MAC-адреса по IP-адресу другого компьютера.  

Что бы не засорять эфир, ответы кэшируются в таблицу ARP. Вот от туда мы и будем брать MAC после пинга IP адреса. Для этого есть специальная утилита в операционной системе — ARP.  

- Парсим таблицу.
- Модифицируем функцию сканирования.  
- Добавляем доменные имена компов если они есть.

3. Клиент программа реализует клиент-сервер со стороны клиента.   
Реализация сокетов в Python на примере протокола TCP.  
Сокеты в сетевом программировании - структуры данных для конечного подключения.  

> TCP (Transmission Control Protocol) — протокол управления передачей пакетов, расположенный на Транспортном уровне модели OSI. Отвечает за установку соединения, передачу данных, подтверждение получения и повторную передачу данных, в случае их потери.    

Существуют клиентские и серверные сокеты. Вполне легко догадаться что к чему. Серверный сокет прослушивает определенный порт, а клиентский подключается к серверу. После того, как было установлено соединение начинается обмен данными.  

Рассмотрим это на простом примере.  
Представим себе большой зал с множеством небольших окошек, за которыми стоят девушки. Есть и пустые окна, за которыми никого нет. Те самые окна — это порты. Там, где стоит девушка — это открытый порт, за которым стоит какое-то приложение, которое его прослушивает. То есть, если, вы подойдете к окошку с номером 9090, то вас поприветствуют и спросят, чем могут помочь. Так же и с сокетами. 
Создается приложение, которое прослушивает свой порт. Когда клиент устанавливает соединение с сервером на этом порту именно данное приложение будет ответственно за работу этим клиентом. Вы же не подойдете к одному окошку, а кричать вам будут из соседнего.  

После успешной установки соединения сервер и клиент начинают обмениваться информацией. Например, сервер посылает приветствие и предложение ввести какую-либо команду. Клиент в свою очередь вводит команду, сервер ее анализирует, выполняет необходимые операции и отдает клиенту результат.  

4. Program.cs - программа для мониторинга сети в фоновом режиме. Отслеживает главные показатели и сохраняет в логи. 
5. Сервер программа реализация сокетов примере протокола TCP.

В дальнейшем напишу подробно о каждой программе. 

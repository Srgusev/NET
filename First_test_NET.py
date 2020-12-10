import os
import platform
import threading
import socket
from datetime import datetime

def getMyIp():
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) #Создаем сокет (UDP) 
    s.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1) # Настраиваем сокет на BROADCAST вещание.
    s.connect(('<broadcast>', 0))
    return s.getsockname()[0]

def scan_Ip(ip):
    addr = net + str(ip)
    comm = ping_com + addr
    response = os.popen(comm)
    data = response.readlines()
    for line in data:
        if 'TTL' in line:
            print(addr, "--> Ping Ok")
            break

net = getMyIp()
print('You IP :',net)
net_split = net.split('.')
a = '.'
net = net_split[0] + a + net_split[1] + a + net_split[2] + a
start_point = int(input("Введите начальный номер: "))
end_point = int(input("Введите последний номер: "))

oс = platform.system()
if (oс == "Windows"):
    ping_com = "ping -n 1 "
else:
    ping_com = "ping -c 1 "

t1 = datetime.now()
print("Выполняется сканирование:")

for ip in range(start_point, end_point):
    if ip == int(net_split[3]):
       continue
    potoc = threading.Thread(target=scan_Ip, args=[ip])
    potoc.start()

potoc.join()
t2 = datetime.now()
total = t2 - t1

print("Сканирование завершено в: ", total)

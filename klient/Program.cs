using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.Collections.Generic;
using ZstdSharp;
using System.IO;
using System.IO.Enumeration;
using System.Data;

namespace chat_client;

class Client
{
    private static TcpClient tcpClient = new TcpClient("127.0.0.1", 27500);
    //private static TcpClient tcpClient = new TcpClient("213.64.250.75", 27500);
    private static NetworkStream stream = tcpClient.GetStream();

    static void Main(string[] args)
    {
        Console.WriteLine("Welcome to the ChatApp!\n");
        MainMenu(stream);
    }

    private static void MainMenu(NetworkStream stream)
    {
        while (true)
        {
            Console.WriteLine("1. Register user");
            Console.WriteLine("2. Login");
            Console.WriteLine("3. Exit program");

            ConsoleKeyInfo keyInfo = Console.ReadKey(true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.D1:
                    Console.Clear();
                    Console.WriteLine("Register a new user\n");
                    RegisterUser(stream);
                    break;
                case ConsoleKey.D2:
                    Console.Clear();
                    LoginUser(stream);
                    break;
                case ConsoleKey.D3:
                    tcpClient.Close();
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("Invalid input. Please try again.");
                    break;
            }
        }
    }

    private static void RegisterUser(NetworkStream stream)
    {
        while (true)
        {
            Console.Write("Enter username: ");
            string? username = Console.ReadLine();

            Console.Write("Enter password: ");
            string? password = Console.ReadLine();

            if (isValidString(username) && isValidString(password))
            {
                string? registrationData = $"REGISTER.{username},{password}";
                SendToServer(stream, registrationData);
                Console.Clear();

                string replyData = ReadFromServer(stream);
                Console.WriteLine($"{replyData}\n");
                MainMenu(stream);
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Invalid username or password. Comma \",\" and spacebar \" \" are not allowed.\nPlease try again\n");
                MainMenu(stream);
            }
        }
    }

    private static void LoginUser(NetworkStream stream)
    {
        Console.WriteLine("Login with an existing user\n");
        while (true)
        {
            Console.Write("Enter username: ");
            string? username = Console.ReadLine();

            Console.Write("Enter password: ");
            string? password = Console.ReadLine();

            if (isValidString(username) && isValidString(password))
            {
                string? loginData = $"LOGIN.{username},{password}";
                SendToServer(stream, loginData);
                Console.Clear();
                string replyData = ReadFromServer(stream);
                Console.WriteLine($"{replyData}\n");

                if (replyData.Contains("Welcome"))
                {
                    LoggedInMenu(stream, username);
                }
                else
                {
                    return;
                }
            }
            else
            {
                Console.Clear();
                Console.WriteLine("There is no such user in the database. Please try again.\n");
                return;
            }
        }
    }

    private static void LogoutUser(NetworkStream stream, string username)
    {
        string logoutData = $"LOGOUT.{username}";
        SendToServer(stream, logoutData);
        Console.Clear();
        Console.WriteLine("\x1b[3J");
        MainMenu(stream);
    }

    private static void LoggedInMenu(NetworkStream stream, string username)
    {
        bool stopListening = false;

        Thread loginListener = new Thread(() =>
        {
            while (!stopListening)
            {
                if (stream.DataAvailable)
                {
                    string reply = ReadFromServer(stream);
                    if (reply.Contains("has logged"))
                    {
                        Console.WriteLine(reply);
                    }
                }
            }
        });
        loginListener.Start();

        while (true)
        {
            Console.WriteLine("1. Public chat");
            Console.WriteLine("2. Private chat");
            Console.WriteLine("3. Logout\n");

            ConsoleKeyInfo keyInfo = Console.ReadKey(true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.D1:
                    stopListening = true;
                    PublicChat(stream, username);
                    break;
                case ConsoleKey.D2:
                    stopListening = true;
                    PrivateChat(stream, username);
                    break;
                case ConsoleKey.D3:
                    stopListening = true;
                    LogoutUser(stream, username);
                    break;
                default:
                    Console.WriteLine("Invalid input. Please try again.");
                    break;
            }
        }
    }

    private static void SendToServer(NetworkStream stream, string data)
    {
        byte[] messageBuffer = Encoding.ASCII.GetBytes(data);
        stream.Write(messageBuffer, 0, messageBuffer.Length);
    }

    private static string ReadFromServer(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int bytesRead;
        StringBuilder replyDataBuilder = new StringBuilder();

        do
        {
            bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                string partialReplyData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                replyDataBuilder.Append(partialReplyData);

                Array.Clear(buffer, 0, buffer.Length);
            }
        } while (stream.DataAvailable);

        return replyDataBuilder.ToString();
    }

    private static void PublicChat(NetworkStream stream, string username)
    {
        Console.Clear();
        Console.WriteLine("\x1b[3J");

        bool stopListening = false;

        Console.WriteLine("Welcome to the Public Chat!\nType exit to leave.\n");
        Console.WriteLine("Type message");

        Thread publicMessageListener = new Thread(() =>
        {
            while (!stopListening)
            {
                if (stream.DataAvailable)
                {
                    string reply = ReadFromServer(stream);
                    Console.WriteLine($"\n{reply}\n");

                }
            }
        });
        publicMessageListener.Start();

        while (true)
        {
            string message = Console.ReadLine();

            if (message == "exit")
            {
                stopListening = true;
                Console.Clear();
                LoggedInMenu(stream, username);
            }

            string messageData = ($"PUBLIC_MESSAGE.{username},{message}");
            SendToServer(stream, messageData);
        }
    }

    private static void PrivateChat(NetworkStream stream, string username)
    {
        Console.Clear();
        Console.WriteLine("\x1b[3J");

        bool stopListening = false;

        Console.WriteLine("Welcome to the Private Chat!\nType exit to leave.\n");
 
        Thread privateMessageListener = new Thread(() =>
        {
            while (!stopListening)
            {
                if (stream.DataAvailable)
                {
                    string reply = ReadFromServer(stream);
                    Console.WriteLine($"\n{reply}\n");
                }
            }
        });
        privateMessageListener.Start();

        Console.Write("Enter the receivers username: ");
        string receiver = Console.ReadLine();
        Console.Write("Type message: ");

        while (true)
        {
            string message = Console.ReadLine();

            if (message == "exit")
            {
                stopListening = true;
                Console.Clear();
                LoggedInMenu(stream, username);
            }

            string messageData = ($"PRIVATE_MESSAGE.{username},{receiver},{message}");
            SendToServer(stream, messageData);
        }
    }

    private static bool isValidString(string str)
    {
        return !string.IsNullOrWhiteSpace(str) && !str.Contains(" ") && !str.Contains(",");
    }
}
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.IO;
using System.Threading;

namespace EventBusRabbitMQ
{
    public class RabbitMQConnection : IRabbitMQConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection _connection;
        private bool _disposed;

        public RabbitMQConnection(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            if (!IsConnected)
            {
                TryConnect();
            }
        }

        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }

        public bool TryConnect()
        {
            try
            {
                _connection = _connectionFactory.CreateConnection();
            }
            catch (BrokerUnreachableException)
            {
                Thread.Sleep(2000);
                _connection = _connectionFactory.CreateConnection();
            }

            if (IsConnected)
            {
                Console.WriteLine($"RabbitMQ persistent connection acquired a connection {_connection.Endpoint.HostName} and is subscribed to failure events");
                return true;
            }
            else
            {
                Console.WriteLine("FATAL ERROR: RabbitMQ connections could neither be created nor opened");
                return false;
            }
        }

        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connection is available to execute this action!");
            }

            return _connection.CreateModel();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _connection.Close();
                _connection.Dispose();
                _disposed = true;
            }
            catch (IOException e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}

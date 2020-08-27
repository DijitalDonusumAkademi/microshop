using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ordering.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ordering.Infrastructure.Data
{
    public class OrderContextSeed
    {
        public static void Seed(OrderContext orderContext, ILoggerFactory loggerFactory, int retry = 0)
        {
            int retryForAvailability = retry;

            try
            {
                //Applies any pending migrations for the context to the database. Will create the
                //database if it does not already exist.
                //Note that this API is mutually exclusive with DbContext.Database.EnsureCreated().
                //EnsureCreated does not use migrations to create the database and therefore the
                //database that is created cannot be later updated using migrations.
                //Here we use it to automatically migrate docker image of sql server db.
                orderContext.Database.Migrate();

                if (!orderContext.Orders.Any())
                {
                    orderContext.Orders.AddRange(GetPreConfiguredOrders());
                    orderContext.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                if (retryForAvailability++ < 5)
                {
                    var log = loggerFactory.CreateLogger<OrderContextSeed>();
                    log.LogError(ex.Message);
                    Seed(orderContext, loggerFactory, retryForAvailability);
                }
                throw;
            }
        }

        private static IEnumerable<Order> GetPreConfiguredOrders()
        {
            return new List<Order>()
                {
                new Order() { UserName = "swn", FirstName = "Laura", LastName = "Robertson", EmailAddress = "laura@gmail.com", AddressLine = "Amsterdamstraat", TotalPrice = 5239 },
                new Order() { UserName = "swn", FirstName = "Tom", LastName = "Eastwood", EmailAddress ="tom@gmail.com", AddressLine = "Edisonlaan", TotalPrice = 3486 }
            };
        }
    }
}

using System;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Basket.API.Entities;
using Basket.API.Repositories.Interfaces;
using EventBusRabbitMQ;
using EventBusRabbitMQ.Common;
using EventBusRabbitMQ.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Basket.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BasketController : ControllerBase
    {
        private readonly IBasketRepository _repository;
        private readonly EventBusRabbitMQProducer _eventBus;
        private readonly ILogger<BasketController> _logger;
        private readonly IMapper _mapper;

        public BasketController(IBasketRepository repository, EventBusRabbitMQProducer eventBus, ILogger<BasketController> logger, IMapper mapper)
        {
            _repository = repository;
            _eventBus = eventBus;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(BasketCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<BasketCart>> GetBasket(string userName)
        {
            var basket = await _repository.GetBasket(userName);
            return Ok(basket ?? new BasketCart(userName));
        }

        [HttpPost]
        [ProducesResponseType(typeof(BasketCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<BasketCart>> UpdateBasket([FromBody] BasketCart basket)
        {
            var bask = await _repository.UpdateBasket(basket);
            return Ok(bask);
        }

        [HttpDelete("{userName}")]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.NoContent)]
        public async Task<ActionResult<bool>> DeleteBasket(string userName)
        {
            await _repository.DeleteBasket(userName);
            return NoContent();
        }

        //api/v1/basket/checkout
        [Route("[action]")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult> Checkout([FromBody] BasketCheckout basketCheckout)
        {
            // get total price of basket
            // remove basket, because the queue properties include NONautodelete
            // send checkout event to rabbitmq

            var basket = await _repository.GetBasket(basketCheckout.UserName);
            if (basket == null)
            {
                _logger.LogError($"Basket does not exist with the user: {basketCheckout.UserName}");
                return BadRequest();
            }

            // Remove the json string from redis
            bool basketRemoved = await _repository.DeleteBasket(basketCheckout.UserName);
            if (!basketRemoved)
            {
                _logger.LogError($"Basket cannot be deleted!");
                return BadRequest();
            }

            // Once basket is checked out, send an integration event to
            // ordering.api to convert basket to order and proceeds with
            // order creation process
            var eventMessage = _mapper.Map<BasketCheckoutEvent>(basketCheckout);
            eventMessage.RequestId = Guid.NewGuid();
            eventMessage.TotalPrice = basket.TotalPrice;

            try
            {
                _eventBus.PublishBasketCheckout(EventBusConstants.BasketCheckoutQueue, eventMessage);
                return Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Publishing integration event: {EventId} from {AppName}", eventMessage.RequestId, "Basket.API");
                throw;
            }
        }
    }
}

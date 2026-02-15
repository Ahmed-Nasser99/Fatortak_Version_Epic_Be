// SubscriptionsController.cs
using Microsoft.AspNetCore.Mvc;
using fatortak.Services;
using System;
using fatortak.Dtos.Subscription;
using fatortak.Services.SubscriptionService;

namespace fatortak.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<SubscriptionDto>>> GetSubscription()
        {
            try
            {
                var subscription = await _subscriptionService.GetAllSubscriptionAsync();
                return Ok(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting All subscriptions");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscription(Guid id)
        {
            try
            {
                var subscription = await _subscriptionService.GetSubscriptionAsync(id);
                if (subscription == null)
                {
                    return NotFound($"Subscription with ID {id} not found");
                }
                return Ok(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription with ID: {Id}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        // GET: api/subscriptions/tenant/{tenantId}
        [HttpGet("tenant/{tenantId}")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscriptionByTenant(Guid tenantId)
        {
            try
            {
                var subscription = await _subscriptionService.GetSubscriptionByTenantAsync(tenantId);
                if (subscription == null)
                {
                    return NotFound($"Subscription for tenant ID {tenantId} not found");
                }
                return Ok(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription for tenant ID: {TenantId}", tenantId);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        // POST: api/subscriptions
        [HttpPost]
        public async Task<ActionResult<SubscriptionDto>> CreateSubscription([FromBody] CreateSubscriptionDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var subscription = await _subscriptionService.CreateSubscriptionAsync(createDto);
                return CreatedAtAction(
                    nameof(GetSubscription),
                    new { id = subscription.Id },
                    subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription");
                return StatusCode(500, "An error occurred while creating the subscription");
            }
        }
        [HttpPost("{subscriptionId}/reset-ai-usage")]
        public async Task<IActionResult> ResetAiUsage(Guid subscriptionId)
        {
            try
            {
                await _subscriptionService.ResetAiUsageAsync(subscriptionId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting AI usage for subscription ID: {SubscriptionId}", subscriptionId);
                return StatusCode(500, "An error occurred while resetting AI usage");
            }
        }
    }
}
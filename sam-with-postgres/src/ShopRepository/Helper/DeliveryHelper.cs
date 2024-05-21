using Amazon.DynamoDBv2.DataModel;
using ShopRepository.Dtos;
using ShopRepository.Models;
using ShopRepository.Services;

namespace ShopRepository.Helper;

public static class DeliveryHelper
{
    private static readonly NzPostService _nzPostService = new();

    // Generate Delivery Label for an Order

    // TODO perhaps this should use the actual order object instead of the input. 
    // I think delivery label should be generated after payment is successful, so we should have the order object already.
    // that we can input into the function instead of orderInput
    // -- George
    public static async Task<string> GenerateDeliveryLabelAsync(OrderInput orderInput, IDynamoDBContext dbContext)
    {
        // Retrieve Customer details from DynamoDB using CustomerId
        var customer = await dbContext.LoadAsync<Customer>(orderInput.CustomerId);
        if (customer == null) throw new Exception($"Customer with ID {orderInput.CustomerId} not found");

        // Use NZ Post API to generate a delivery label
        var deliveryLabel =
            await _nzPostService.GenerateDeliveryLabelAsync(orderInput.StripeCheckoutSession, orderInput.CustomerId.ToString());

        // Save delivery label to Order in DynamoDB
        var order = await dbContext.LoadAsync<Order>(new Guid(orderInput.StripeCheckoutSession));
        if (order == null) throw new Exception($"Order with PaymentIntentId {orderInput.StripeCheckoutSession} not found");

        order.DeliveryLabelUid = deliveryLabel;
        await dbContext.SaveAsync(order);

        return deliveryLabel;
    }

    // Track Order status using NZ Post
    public static async Task<string> TrackOrderAsync(string trackingId)
    {
        return await _nzPostService.TrackOrderAsync(trackingId);
    }

    // Estimate Delivery Date based on current date and shipping method
    public static async Task<DateTime> EstimateDeliveryDateAsync(DateTime orderDate, string shippingMethod)
    {
        return await _nzPostService.EstimateDeliveryDateAsync(shippingMethod);
    }
}
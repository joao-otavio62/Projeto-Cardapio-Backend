namespace PizzaFlow.Domain.Enums;

public enum OrderStatus { PendingPayment, Received, Preparing, OutForDelivery, ReadyForPickup, Finished, Cancelled }
public enum DeliveryType { Pickup, Delivery }
public enum PaymentMethod { Pix, CreditCard }
public enum PaymentStatus { Pending, Approved, Rejected, Cancelled, Refunded, Expired }
public enum MediaType { Image, Video }
public enum DiscountType { Percent, Fixed }
public enum StockReason { Sale, Adjustment, Restock, Cancellation }

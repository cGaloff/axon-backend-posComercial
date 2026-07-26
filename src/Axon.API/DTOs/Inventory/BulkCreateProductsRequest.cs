namespace Axon.API.DTOs.Inventory;

public record BulkCreateProductsRequest(List<CreateProductRequest> Products);

using MediatR;

namespace Axon.Application.Suppliers.Queries;

// Al seleccionar un proveedor en el formulario de compra, el frontend consulta
// este endpoint para autocompletar el resto de sus datos (contacto, teléfono,
// correo, dirección, tipo/número de documento) sin obligar al usuario a
// reingresar información que el proveedor ya tiene registrada.
public record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierDto>;

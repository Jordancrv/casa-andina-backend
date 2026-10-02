# Modelo normalizado de personas y huéspedes

## Objetivo

Separar la cuenta de acceso (`Cliente`) de la identidad de la persona, sus
documentos y sus datos de contacto. Una reserva puede incluir uno o varios
huéspedes, aunque solo una cuenta cliente haya realizado la operación.

## Entidades

| Entidad | Responsabilidad |
|---|---|
| `Persona` | Raíz de identidad y discriminador natural/jurídica. |
| `PersonaNatural` | Nombres y datos propios de una persona física. |
| `PersonaJuridica` | Razón social y nombre comercial de una empresa. |
| `TipoDocumento` | Catálogo: DNI, CE, PASAPORTE y RUC. |
| `PersonaDocumento` | Documentos de una persona; admite más de uno y uno principal. |
| `PersonaCorreo` | Correos independientes; uno principal y unicidad global. |
| `PersonaTelefono` | Teléfonos de contacto; uno principal por persona. |
| `Cliente` | Credenciales y perfil de acceso vinculados 1:1 con `Persona`. |
| `ReservaHuesped` | Relación N:M entre una reserva y sus huéspedes naturales. |

## Relaciones principales

- `Persona 1:0..1 PersonaNatural` o `Persona 1:0..1 PersonaJuridica`.
- `Persona 1:N PersonaDocumento`.
- `Persona 1:N PersonaCorreo`.
- `Persona 1:N PersonaTelefono`.
- `Cliente 1:1 Persona`.
- `Reserva N:M PersonaNatural`, mediante `ReservaHuesped`.
- Cada reserva puede tener como máximo un huésped titular.

## Decisiones de integridad

- Un documento no puede repetirse para el mismo tipo documental.
- Un correo no puede pertenecer a dos personas.
- Solo puede existir un documento, correo y teléfono principal por persona.
- `ReservaHuesped` referencia `PersonaNatural`, porque una empresa puede pagar o
  ser titular comercial, pero no alojarse físicamente.
- Se incluye RUC para identificar personas jurídicas.

## Transición

La migración conserva temporalmente `NombreCompleto`, `DNI`, `Correo` y
`Telefono` en `Cliente`. Estas columnas se consideran heredadas y se retirarán
cuando registro, autenticación, CRM y reservas usen exclusivamente las nuevas
relaciones. Esto permite desplegar la base antes de cambiar todas las pantallas
sin interrumpir el sistema actual.

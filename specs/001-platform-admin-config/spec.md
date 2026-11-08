# Feature Specification: Administración de plataforma y configuración empresarial

**Feature Branch**: `Not created`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Diseñar la funcionalidad fundacional de ELANGI ERP para administrar la plataforma, empresas, usuarios, membresías, roles y permisos, sucursales, configuración regional, terceros y auditoría, con aislamiento estricto entre compañías. No implementar módulos operativos ni generar código o migraciones en esta etapa."

## Clarifications

### Session 2026-11-02

- Q: ¿Puede una persona con rol Platform Owner tener también una membresía empresarial explícita como Company Administrator o Company User? → A: Sí; puede tener membresía explícita, con permisos empresariales separados del rol global.
- Q: ¿Qué debe ocurrir con una invitación aún no aceptada cuando el administrador la revoca? → A: Se invalida la invitación y la membresía pasa a Revoked; un nuevo intento crea otra invitación y membresía.
- Q: Si una empresa habilita varias monedas, ¿cómo debe configurarse la precisión decimal de los valores monetarios? → A: Configurable por empresa y por cada moneda habilitada, independiente de las demás categorías decimales.
- Q: ¿Cómo debe relacionarse una serie de documentos con las sucursales de su empresa? → A: Cada serie pertenece obligatoriamente a una sucursal; una sucursal puede tener varias series. Son entidades distintas.
- Q: ¿Qué debe ocurrir con el número de un documento que se anula después de haber sido numerado? → A: Conservar el número como anulado y no reutilizarlo; los rechazos o fallos anteriores a completar la emisión no consumen número.
- Q: ¿Qué formato debe tener el número emitido para cada combinación de empresa, tipo de documento y serie? → A: Prefijo y relleno con ceros configurables por combinación; inicia en 1 y no se reinicia periódicamente.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar y administrar empresas (Priority: P1)

Como Platform Owner, quiero registrar empresas, consultar su estado, activarlas o suspenderlas,
designar su administrador inicial y consultar información general de suscripción para administrar
el acceso a la plataforma.

**Why this priority**: Las empresas y su estado son la base para habilitar cualquier espacio de
trabajo empresarial.

**Independent Test**: Registrar una empresa, asignar un administrador inicial, consultar sus
datos y cambiar su estado sin acceder a datos operativos empresariales.

**Acceptance Scenarios**:

1. **Given** un Platform Owner autorizado, **When** registra una empresa con sus datos de
   identificación requeridos, **Then** la empresa queda registrada con un identificador único y
   un estado visible.
2. **Given** una empresa registrada, **When** el Platform Owner la activa o suspende, **Then** el
   nuevo estado queda guardado y se refleja en las consultas posteriores.
3. **Given** una empresa sin administrador inicial, **When** el Platform Owner designa uno,
   **Then** se inicia la invitación con rol de Company Administrator para esa empresa y sin
   conceder acceso antes de su aceptación.
4. **Given** información general de suscripción, **When** el Platform Owner la consulta o
   actualiza, **Then** puede ver el plan y estado informativos sin iniciar cobros ni facturación.
5. **Given** un usuario que no es Platform Owner, **When** intenta administrar empresas de la
   plataforma, **Then** la acción se rechaza y no se modifica información.

---

### User Story 2 - Invitar usuarios y administrar membresías (Priority: P1)

Como Company Administrator, quiero invitar personas y administrar sus membresías para controlar
quién puede acceder a mi empresa sin afectar sus membresías en otras empresas.

**Why this priority**: El acceso empresarial requiere membresías explícitas, que pueden ser
distintas para cada empresa de una misma persona.

**Independent Test**: Invitar a una persona, aceptar la invitación, cambiar el estado de su
membresía y verificar que las membresías de otras empresas permanecen intactas.

**Acceptance Scenarios**:

1. **Given** un Company Administrator y una empresa activa, **When** invita a una dirección de
   correo, **Then** se crea una membresía Pending asociada exclusivamente a su empresa.
2. **Given** una invitación vigente, **When** el destinatario se autentica con la identidad
   correspondiente y la acepta, **Then** la membresía pasa a Active.
3. **Given** un usuario con membresías Active en varias empresas, **When** establece una de ellas
   como predeterminada, **Then** esa empresa queda seleccionada al iniciar el acceso empresarial.
4. **Given** una membresía Active, **When** su Company Administrator la suspende o revoca,
   **Then** el acceso a esa empresa se bloquea, mientras las otras membresías del usuario no
   cambian.
5. **Given** una membresía Pending, Suspended o Revoked, **When** intenta acceder a los recursos
   empresariales, **Then** se deniega el acceso.
6. **Given** una invitación aún no aceptada, **When** un administrador autorizado la revoca,
   **Then** la invitación queda invalidada y la membresía pasa a Revoked; aceptar esa invitación
   posteriormente no reactiva la membresía.
7. **Given** el último Company Administrator activo de una empresa, **When** intenta suspender,
   revocar o degradar su propia membresía, **Then** la operación se rechaza hasta que exista otro
   administrador activo.

---

### User Story 3 - Administrar roles, permisos y límites de acceso (Priority: P1)

Como administrador, quiero asignar roles y permisos dentro del alcance que administro para
proteger cada empresa y mantener separadas las responsabilidades globales y empresariales.

**Why this priority**: Sin autorización por rol y empresa no se puede proteger la información
multiempresa ni delegar la administración.

**Independent Test**: Asignar roles y permisos en una empresa y comprobar tanto las acciones
permitidas como la denegación de acciones fuera del rol o empresa.

**Acceptance Scenarios**:

1. **Given** una empresa, **When** se asignan Company Administrator o Company User y sus
   permisos, **Then** esos roles y permisos aplican únicamente a esa empresa.
2. **Given** un Company Administrator autorizado, **When** crea o actualiza un rol propio de la
   empresa y le asigna permisos disponibles, **Then** ese rol y sus permisos existen únicamente
   dentro de esa empresa.
3. **Given** un Company Administrator, **When** administra usuarios, roles o permisos, **Then**
   solo puede hacerlo dentro de su empresa y conforme a sus permisos.
4. **Given** un administrador de una empresa, **When** intenta leer o modificar membresías,
   roles o permisos de otra empresa, **Then** la operación se deniega sin revelar datos de la
   otra empresa.
5. **Given** un Platform Owner, **When** realiza administración global de la plataforma, **Then**
   puede administrar metadatos de empresas pero no obtiene por ese rol acceso a sus datos
   operativos; solo puede acceder a una empresa si tiene una membresía explícita allí, cuyos
   permisos se verifican de forma independiente.
6. **Given** cualquier solicitud, **When** falla la comprobación de pertenencia o permiso,
   **Then** no se expone ni modifica información protegida.

---

### User Story 4 - Administrar sucursales empresariales (Priority: P2)

Como Company Administrator, quiero registrar y mantener las sucursales de mi empresa para
representar sus ubicaciones sin mezclar datos con otras empresas.

**Why this priority**: Las sucursales completan la estructura empresarial necesaria para futuros
módulos, aunque no habilitan por sí mismas operaciones comerciales.

**Independent Test**: Crear y consultar sucursales de dos empresas y comprobar que cada una solo
puede administrar las propias.

**Acceptance Scenarios**:

1. **Given** una empresa activa, **When** su Company Administrator registra o actualiza una
   sucursal, **Then** la sucursal queda asociada exclusivamente a esa empresa.
2. **Given** una sucursal de otra empresa, **When** un usuario intenta consultarla o modificarla,
   **Then** la operación se deniega.
3. **Given** una empresa suspendida, **When** un usuario empresarial intenta administrar sus
   sucursales, **Then** no puede realizar operaciones empresariales.

---

### User Story 5 - Configurar parámetros regionales y precisión (Priority: P2)

Como Company Administrator, quiero configurar los parámetros regionales de mi empresa para que
sus usuarios vean y registren datos con los valores y formatos correctos.

**Why this priority**: Las empresas operan en distintas regiones y requieren convenciones
coherentes antes de incorporar módulos operativos.

**Independent Test**: Guardar todas las opciones regionales de una empresa y verificar que se
recuperan sin cambiar la configuración de otra empresa.

**Acceptance Scenarios**:

1. **Given** una empresa, **When** su Company Administrator establece país, idioma, cultura,
   zona horaria y formato de fecha válidos, **Then** esos valores se guardan para esa empresa.
2. **Given** una empresa, **When** establece una moneda principal y monedas adicionales,
   **Then** la moneda principal se identifica separadamente y la lista adicional queda disponible
   para esa empresa.
3. **Given** una empresa, **When** establece separadores decimal y de miles distintos,
   **Then** los valores se guardan y se presentan de acuerdo con esa configuración.
4. **Given** una empresa, **When** configura precisión decimal para cantidades, precios,
   porcentajes y tasas de cambio, **Then** cada precisión se guarda independientemente; para
   valores monetarios, se guarda una precisión independiente por cada moneda habilitada.
5. **Given** valores regionales inválidos o separadores iguales, **When** se intenta guardar la
   configuración, **Then** la operación se rechaza con un error comprensible y no se guardan
   valores parcialmente.
6. **Given** dos empresas, **When** una modifica sus parámetros regionales, **Then** la
   configuración de la otra empresa no cambia.

---

### User Story 6 - Administrar terceros (Priority: P2)

Como Company Administrator, quiero registrar y mantener clientes y proveedores para que cada
empresa gestione sus relaciones comerciales de forma independiente.

**Why this priority**: El registro común de terceros prepara la estructura para los módulos
comerciales futuros sin implementar sus operaciones.

**Independent Test**: Registrar un tercero como cliente, proveedor y ambos, y comprobar que dos
empresas pueden mantener registros independientes para el mismo tercero.

**Acceptance Scenarios**:

1. **Given** una empresa activa, **When** su usuario autorizado registra un tercero como cliente,
   proveedor o ambos, **Then** el tercero queda disponible con los tipos seleccionados únicamente
   en esa empresa.
2. **Given** dos empresas, **When** cada una registra datos para una misma persona u
   organización externa, **Then** cada empresa puede mantener y consultar su registro
   independiente.
3. **Given** un tercero perteneciente a otra empresa, **When** un usuario intenta consultarlo o
   modificarlo, **Then** la operación se deniega.

---

### User Story 7 - Consultar auditoría administrativa (Priority: P1)

Como administrador autorizado, quiero consultar el historial de operaciones administrativas y
sensibles para poder rastrear quién realizó cada cambio y en qué contexto.

**Why this priority**: La trazabilidad es necesaria desde el inicio para revisar cambios de
acceso, configuración y administración de empresas.

**Independent Test**: Ejecutar operaciones administrativas exitosas y verificar que cada una
produce un registro consultable dentro del ámbito autorizado; el Platform Owner puede supervisar
metadatos de auditoría globales sin acceder a datos operativos empresariales.

**Acceptance Scenarios**:

1. **Given** una operación administrativa o sensible exitosa, **When** se completa, **Then**
   queda registrada con usuario, empresa cuando corresponda, operación, fecha y datos relevantes
   del cambio.
2. **Given** un Company Administrator, **When** consulta auditoría, **Then** solo puede consultar
   registros de su empresa.
3. **Given** un Platform Owner autenticado, **When** consulta
   `GET /api/v1/platform/audit-records`, **Then** puede revisar eventos de plataforma y eventos
   administrativos/sensibles de empresas con filtros y paginación, sin obtener acceso a datos
   operativos empresariales ni a contenidos de documentos.
4. **Given** un usuario sin permiso de auditoría, **When** intenta consultar o alterar registros,
   **Then** la consulta o modificación se deniega y el historial existente permanece íntegro.
5. **Given** un cambio auditado, **When** sus datos incluyen credenciales o secretos, **Then**
   estos valores no aparecen en el historial.

---

### User Story 8 - Configurar permisos y numeración de documentos (Priority: P2)

Como Company Administrator, quiero administrar tipos de documento, series de emisión y permisos
por membresía para que cada persona pueda emitir únicamente los documentos autorizados desde las
sucursales de mi empresa. Como usuario autorizado, quiero recibir un número único para el tipo y
la serie seleccionados.

**Why this priority**: Las reglas de autorización y numeración son una base común para futuros
módulos que emitirán documentos, sin implementar todavía sus flujos operativos.

**Independent Test**: Configurar una sucursal con varias series y varios tipos de documento,
asignar permisos distintos por membresía y verificar los números asignados, el aislamiento entre
empresas y el rechazo de emisiones no autorizadas.

**Acceptance Scenarios**:

1. **Given** una empresa con sucursales, **When** un Company Administrator crea series de
   emisión, **Then** cada serie pertenece a exactamente una sucursal de esa misma empresa y una
   sucursal puede tener varias series independientes.
2. **Given** un tipo de documento y una serie de la misma empresa, **When** la serie se habilita
   para ese tipo, **Then** puede utilizarse para emitir ese tipo sin impedir que la serie se use
   también para otros tipos habilitados.
3. **Given** una membresía empresarial, **When** su Company Administrator configura permisos de
   emisión, **Then** cada autorización queda limitada a esa membresía, un tipo de documento y una
   serie de la empresa.
4. **Given** dos tipos de documento habilitados en la misma serie, **When** una membresía solo
   tiene permiso para uno, **Then** puede emitir ese tipo y se le deniega el otro.
5. **Given** una membresía activa autorizada, **When** emite un documento con una empresa, tipo
   y serie permitidos, **Then** recibe un número del consecutivo reservado para esa combinación.
6. **Given** varias emisiones simultáneas para la misma empresa, tipo y serie, **When** se
   asignan los números, **Then** cada documento recibe un número distinto y el consecutivo avanza
   sin duplicados.
7. **Given** identificadores de empresa, tipo o serie proporcionados por el cliente, **When** el
   backend valida una emisión, **Then** verifica el contexto empresarial, la pertenencia y el
   permiso efectivo sin confiar en esos identificadores como prueba de autorización.
8. **Given** una serie, tipo, sucursal o membresía de otra empresa, **When** se intenta
   combinarlos para configurar permisos o emitir un documento, **Then** se rechaza la operación
   sin exponer datos ni consumir un número.
9. **Given** un documento emitido con un número asignado, **When** se anula posteriormente,
   **Then** conserva su número y estado de anulación en el historial, y el número no vuelve a
   estar disponible para otra emisión.
10. **Given** una emisión que falla antes de completarse, **When** se revierte la operación,
    **Then** no se consume ni se pierde un número del consecutivo.
11. **Given** una combinación de empresa, tipo de documento y serie con formato configurado,
    **When** se emiten documentos, **Then** los números comienzan en 1, aplican el prefijo y
    relleno con ceros configurados y continúan sin reinicio periódico.

---

### User Story 9 - Administrar el logo de la empresa (Priority: P2)

Como Company Administrator autorizado, quiero cargar, reemplazar y eliminar el logo de mi empresa
para identificarla en la interfaz sin exponer archivos ni referencias de otras empresas.

**Why this priority**: El logo proporciona una identidad visual opcional por empresa y debe
respetar los controles de acceso y el aislamiento multiempresa.

**Independent Test**: Cargar un logo válido, reemplazarlo, eliminarlo y verificar su referencia,
lectura pública, auditoría y aislamiento frente a otra empresa.

**Acceptance Scenarios**:

1. **Given** una empresa nueva, **When** se crea sin cargar un logo, **Then** su
   `LogoStoragePath` permanece null y la creación se completa normalmente.
2. **Given** una membresía Active con el permiso empresarial apropiado, **When** carga un PNG,
   JPG/JPEG o WebP de hasta 2 MB, **Then** el backend valida el archivo, lo almacena en el bucket
   `company-logos` bajo una ruta de esa empresa, actualiza `LogoStoragePath` y audita la operación.
3. **Given** un archivo de formato no permitido o mayor de 2 MB, **When** intenta cargarlo,
   **Then** el backend rechaza la solicitud sin cambiar la referencia existente.
4. **Given** un logo existente, **When** el usuario autorizado lo reemplaza, **Then** la empresa
   referencia el nuevo archivo y el anterior se elimina sin dejar una referencia obsoleta; la
   operación queda auditada.
5. **Given** un logo existente, **When** el usuario autorizado lo elimina, **Then**
   `LogoStoragePath` queda null, el archivo se elimina y la operación queda auditada.
6. **Given** un usuario sin permiso o con membresía inactiva, **When** intenta cargar,
   reemplazar o eliminar un logo, **Then** la operación se deniega sin modificar la empresa ni
   el almacenamiento.
7. **Given** una empresa A, **When** un usuario autorizado de A intenta cambiar el logo de la
   empresa B, **Then** el backend deniega la operación y no revela ni modifica el archivo de B.
8. **Given** un logo almacenado, **When** el cliente lo presenta, **Then** puede leerlo
   públicamente desde el bucket sin que la entidad Company almacene una URL fija ni el binario.

### Edge Cases

- Una invitación ya aceptada, revocada, vencida o enviada a una identidad distinta no puede
  activarse mediante aceptación repetida o por otra persona.
- Revocar una invitación Pending invalida esa invitación y revoca terminalmente la membresía;
  una nueva invitación crea otra membresía y no reutiliza la revocada.
- No se permite dejar una empresa activa sin Company Administrator activo al suspender, revocar o
  degradar al último administrador.
- Suspender una empresa impide el acceso empresarial aunque la membresía individual siga Active;
  al reactivarla, solo las membresías Active vuelven a tener acceso.
- Suspender o revocar una membresía no cambia las membresías del usuario en otras empresas ni
  permite mantener como predeterminada una empresa a la que ya no puede acceder.
- Una empresa no puede seleccionar como moneda principal una moneda que no esté habilitada para
  esa empresa.
- Cada moneda habilitada mantiene su propia precisión monetaria; cambiarla no modifica la
  precisión monetaria de otras monedas ni las precisiones de cantidades, precios, porcentajes o
  tasas de cambio.
- Una serie no puede utilizarse con un tipo de documento en una empresa distinta ni vincularse a
  una sucursal de otra empresa.
- Tener permiso para un tipo de documento en una serie no concede permiso para los otros tipos
  asociados a esa misma serie.
- Cambiar identificadores enviados por el cliente no permite emitir desde otra empresa, serie o
  tipo de documento; el backend comprueba la autorización contra la membresía autenticada.
- Las emisiones concurrentes del mismo ámbito de numeración reciben números únicos; una
  operación no autorizada o con referencias incompatibles no consume un número.
- Un número de un documento anulado permanece asociado al documento anulado y nunca se reutiliza;
  un fallo o rechazo anterior a completar la emisión no consume un número.
- El prefijo y el relleno con ceros se configuran para cada combinación de empresa, tipo y serie;
  el contador comienza en 1 y no se reinicia periódicamente.
- Configuraciones regionales incompletas o incompatibles no se guardan parcialmente.
- El logo es opcional; formatos distintos de PNG, JPG/JPEG o WebP y archivos mayores de 2 MB se
  rechazan en el backend.
- La ruta del logo siempre queda dentro del espacio de almacenamiento de su empresa; una
  sustitución no deja la referencia apuntando al archivo anterior y una eliminación deja la
  referencia en null.
- Una falla al actualizar la referencia o auditar una carga no debe reportar éxito ni descartar
  la referencia al logo anterior; las fallas de limpieza del archivo deben exponerse y no
  ocultarse.
- Un tercero de otra empresa no puede descubrirse mediante búsquedas, listados, identificadores o
  referencias indirectas.
- La indisponibilidad de auditoría no debe producir una operación sensible exitosa sin registro
  de auditoría.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir al Platform Owner registrar, consultar y actualizar los
  datos generales e identificativos de una empresa, incluidos nombre comercial, razón social,
  país, identificador legal cuando aplique, dirección y datos de contacto.
- **FR-002**: El sistema MUST permitir al Platform Owner activar y suspender empresas y mostrar
  claramente su estado actual.
- **FR-003**: El sistema MUST permitir asociar información general de suscripción a una empresa,
  incluida su referencia de plan y estado informativo, sin cobrar, emitir facturas ni procesar
  pagos.
- **FR-004**: El sistema MUST permitir al Platform Owner designar un Company Administrator
  inicial mediante el flujo de invitación empresarial.
- **FR-005**: El sistema MUST permitir que una persona tenga membresías independientes en varias
  empresas y que cada membresía tenga uno de los estados Pending, Active, Suspended o Revoked.
- **FR-006**: El sistema MUST permitir a un administrador autorizado invitar a una persona por
  correo electrónico y asociar la invitación a una sola empresa y rol inicial. MUST permitir
  revocar invitaciones no aceptadas, invalidarlas y cambiar su membresía asociada a Revoked;
  una invitación revocada MUST NOT poder aceptarse.
- **FR-007**: El sistema MUST activar una membresía Pending únicamente después de que su
  destinatario autenticado acepte una invitación vigente dirigida a su identidad.
- **FR-008**: El sistema MUST permitir suspender y revocar membresías, bloquear su acceso al
  ámbito empresarial y preservar sin cambios las membresías de la persona en otras empresas.
- **FR-009**: El sistema MUST permitir establecer como empresa predeterminada una membresía
  Active y MUST impedir que una membresía no activa permanezca como predeterminada.
- **FR-010**: El sistema MUST exigir que cada empresa mantenga al menos un Company Administrator
  activo y MUST rechazar operaciones que dejen una empresa sin ninguno.
- **FR-011**: El sistema MUST aplicar los roles Platform Owner, Company Administrator y
  Company User según sus respectivos ámbitos y permisos.
- **FR-012**: El sistema MUST mantener independientes por empresa las asignaciones de roles y
  permisos y permitir a administradores autorizados crear roles empresariales y administrar sus
  asignaciones únicamente dentro de su empresa.
- **FR-013**: El sistema MUST denegar toda lectura o modificación de datos empresariales cuando
  no se verifiquen membresía activa y permiso para la operación.
- **FR-014**: El rol Platform Owner MUST permitir administrar metadatos globales de empresas,
  pero MUST NOT conceder por sí mismo acceso a datos operativos empresariales. Si una persona
  Platform Owner también pertenece a una empresa, MUST contar con una membresía explícita y
  permisos empresariales independientes para acceder a sus datos.
- **FR-015**: El sistema MUST permitir administrar sucursales pertenecientes a una sola empresa,
  con nombre, ubicación y datos generales de contacto, y MUST aislar su consulta y modificación
  por empresa.
- **FR-016**: El sistema MUST permitir configurar independientemente por empresa país, moneda
  principal, monedas adicionales, idioma, cultura, zona horaria y formato de fechas.
- **FR-017**: El sistema MUST permitir configurar separadores decimal y de miles y una precisión
  decimal independiente para cantidades, precios, porcentajes y tasas de cambio, así como una
  precisión monetaria independiente para cada moneda habilitada por la empresa.
- **FR-018**: El sistema MUST validar la compatibilidad de las opciones regionales y rechazar la
  persistencia parcial de configuraciones inválidas.
- **FR-019**: El sistema MUST permitir a cada empresa administrar terceros con nombre e
  información general de identificación y contacto, clasificados como cliente, proveedor o
  ambos.
- **FR-020**: El sistema MUST permitir que una misma persona u organización externa tenga
  registros de tercero independientes en diferentes empresas.
- **FR-021**: El sistema MUST registrar las operaciones administrativas y sensibles exitosas con
  usuario, empresa cuando corresponda, operación, fecha y datos relevantes del cambio, excluyendo
  credenciales y secretos.
- **FR-022**: El sistema MUST restringir la consulta de auditoría al ámbito y permisos del
  solicitante y MUST impedir la alteración de registros existentes. Un Platform Owner MUST poder
  consultar metadatos de eventos de plataforma y eventos administrativos/sensibles de empresas
  mediante el endpoint global definido en el contrato de API, sin que esta capacidad le conceda
  acceso a datos operativos empresariales ni a contenidos de documentos.
- **FR-023**: El sistema MUST impedir que una operación sensible se complete exitosamente si no
  se puede registrar su auditoría correspondiente.
- **FR-024**: El sistema MUST mantener autenticación mediante la identidad de usuario aprobada
  para ELANGI y no almacenar contraseñas en perfiles empresariales.
- **FR-025**: El sistema MUST dejar fuera de alcance facturación, procesamiento de pagos y los
  flujos operativos completos de los módulos comerciales, inventario, compras, ventas, cartera,
  tesorería y contabilidad. El catálogo de tipos de documento, las series, los permisos de
  emisión y la asignación fundacional de números consecutivos están dentro del alcance.
- **FR-026**: El sistema MUST representar en `DocumentSources` los tipos de documento, incluidos
  Invoice, CreditNote, PurchaseOrder, DebitNote, CashReceipt y PaymentVoucher, sin confundirlos
  con sucursales o series.
- **FR-027**: El sistema MUST representar en `DocumentSeries` las series o sedes de emisión como
  entidades distintas de `Branches`. Cada serie MUST pertenecer a exactamente una sucursal de la
  misma empresa; una sucursal MAY tener varias series.
- **FR-028**: El sistema MUST permitir habilitar una misma `DocumentSeries` para varios
  `DocumentSources`, conservando configuraciones y consecutivos independientes por tipo.
- **FR-029**: El sistema MUST mantener en `DocumentNumberSequences` un consecutivo independiente
  para cada combinación `CompanyId + DocumentSourceId + DocumentSeriesId`.
- **FR-030**: El sistema MUST asignar cada número de forma atómica y transaccional, de modo que
  emisiones concurrentes de la misma combinación no reciban números duplicados. La asignación
  MUST confirmarse junto con la emisión; si esta falla antes de completarse, el consecutivo no
  avanza.
- **FR-031**: El sistema MUST permitir asignar permisos de emisión por membresía empresarial,
  tipo de documento y serie, mediante una relación equivalente a
  `CompanyUserDocumentSeries`.
- **FR-032**: Una autorización para un tipo de documento y serie MUST NOT otorgar autorización
  para otros tipos de documento asociados a esa serie.
- **FR-033**: Antes de emitir o asignar un número, el backend MUST verificar la identidad, la
  membresía activa, la empresa contextual, la compatibilidad entre empresa, tipo, serie y
  sucursal, y el permiso específico de emisión. MUST NOT confiar en identificadores recibidos
  del frontend para establecer el contexto ni autorizar la operación.
- **FR-034**: El sistema MUST rechazar combinaciones de membresía, empresa, tipo, serie o
  sucursal que no pertenezcan al mismo contexto empresarial, sin revelar datos de otra empresa
  ni consumir un número.
- **FR-035**: Si un documento se anula después de emitirse, el sistema MUST conservar su número
  asociado al documento anulado y MUST NOT reutilizarlo. La anulación MUST quedar registrada en
  auditoría.
- **FR-036**: El sistema MUST permitir configurar un prefijo y el relleno con ceros del número
  para cada combinación empresa, tipo de documento y serie. Cada consecutivo MUST iniciar en 1,
  avanzar continuamente y MUST NOT reiniciarse por períodos.
- **FR-037**: El logo de la empresa MUST ser opcional; el sistema MUST permitir cargar,
  reemplazar y eliminarlo mediante `PUT /companies/{companyId}/logo` y
  `DELETE /companies/{companyId}/logo`. Estas operaciones MUST requerir membresía Active y el
  permiso empresarial apropiado; el backend MUST validar el archivo y el contexto autorizado.
- **FR-038**: El logo MUST almacenarse como objeto del bucket público de lectura `company-logos`,
  en una ruta aislada por empresa. El backend MUST aceptar únicamente PNG, JPG/JPEG y WebP de
  hasta 2 MB, controlar las operaciones de escritura y eliminación, actualizar
  `LogoStoragePath` al reemplazar, dejarlo null al eliminar y auditar cada cambio exitoso. La
  entidad Company MUST NOT almacenar el contenido binario ni una URL fija del logo.

### Key Entities

- **Platform Owner**: Persona autorizada para administrar metadatos y configuración global de la
  plataforma. Puede tener membresías empresariales explícitas, pero su rol global no concede
  acceso operativo a ninguna empresa.
- **Company**: Organización cliente con nombre comercial, razón social, país, identificador
  legal cuando aplique, dirección, contacto, estado, información general de suscripción y
  configuración propia, incluida una ruta opcional al objeto de su logo.
- **User Profile**: Perfil de aplicación vinculado a una identidad autenticada, sin credenciales
  almacenadas en el perfil empresarial.
- **Membership**: Relación de una persona con una empresa, con estado, roles asignados y posible
  condición de empresa predeterminada para esa persona.
- **Invitation**: Invitación dirigida a una identidad para crear o activar una membresía en una
  empresa y con un rol inicial. Si se revoca antes de aceptarse, queda invalidada y la membresía
  asociada pasa a Revoked; una nueva invitación crea otra membresía.
- **Role and Permission**: Rol base o rol empresarial creado desde permisos disponibles, asignable
  a membresías y aislado de los roles de otras empresas.
- **Branch**: Ubicación con nombre, dirección y contacto, perteneciente a una única empresa.
- **Regional Configuration**: Preferencias independientes de país, monedas, idioma, cultura,
  zona horaria, formato y precisión de una empresa; incluye una precisión monetaria por cada
  moneda habilitada.
- **Third Party**: Persona u organización con datos de identificación y contacto, registrada de
  forma independiente por una empresa como cliente, proveedor o ambos.
- **Audit Record**: Historial protegido de una operación administrativa o sensible y su actor,
  contexto y cambio relevante.
- **Subscription Information**: Información descriptiva de plan y estado asociada a una empresa,
  sin cargos ni facturación.
- **DocumentSource**: Tipo de documento que puede emitirse, como Invoice o PurchaseOrder; no
  representa una sucursal ni una serie.
- **DocumentSeries**: Serie o sede de emisión distinta de una sucursal, asociada obligatoriamente
  a exactamente una sucursal de la misma empresa. Una sucursal puede tener varias series, y una
  serie puede habilitarse para varios tipos de documento.
- **DocumentNumberSequence**: Consecutivo independiente por empresa, tipo de documento y serie,
  asignado de forma atómica para evitar duplicados concurrentes; inicia en 1, no se reinicia
  periódicamente y admite prefijo y relleno con ceros configurables para su combinación.
- **CompanyUserDocumentSeries**: Autorización de emisión asociada a una membresía empresarial,
  un tipo de documento y una serie. La autorización se evalúa para esa combinación exacta.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los casos de prueba de acceso cruzado a empresas para membresías,
  sucursales, configuración, terceros, logos, series, permisos de emisión y auditoría deniegan
  lectura y modificación no autorizadas.
- **SC-002**: En pruebas de aceptación, el Platform Owner puede registrar una empresa, designar
  su administrador inicial, consultar su estado y cambiarlo sin acceso a datos operativos.
- **SC-003**: El 100% de las operaciones administrativas y sensibles exitosas de la matriz de
  aceptación contiene usuario, fecha, operación y empresa cuando corresponda, además del cambio
  relevante no sensible.
- **SC-004**: El 100% de los casos de aceptación de invitación y transición entre los estados
  Pending, Active, Suspended y Revoked produce el estado esperado y no modifica membresías de
  otras empresas; solo una membresía Active puede ser predeterminada.
- **SC-005**: La configuración regional completa puede guardarse y consultarse de forma
  independiente para dos empresas, y el 100% de las configuraciones incompatibles de la matriz es
  rechazado sin persistencia parcial. Una empresa puede configurar precisiones monetarias
  distintas para dos monedas sin cambiar las precisiones de las otras categorías.
- **SC-006**: En pruebas de aceptación, los usuarios autorizados pueden completar los escenarios
  primarios de las nueve historias; los usuarios no autorizados no completan ninguna acción
  protegida.
- **SC-007**: Ningún escenario de aceptación crea cargos, factura una suscripción ni ejecuta
  operaciones de los módulos declarados fuera de alcance.
- **SC-008**: En pruebas de usabilidad, al menos el 90% de los Platform Owners completa el flujo
  de registro de empresa, designación de administrador y consulta o cambio de estado en cinco
  minutos o menos, sin acceder a datos operativos empresariales.
- **SC-009**: El 100% de las asignaciones de números en las pruebas concurrentes conserva
  unicidad y pertenece a la combinación de empresa, tipo de documento y serie solicitada; el
  formato usa el prefijo y el relleno configurados y el contador no se reinicia periódicamente.
- **SC-010**: El 100% de las emisiones sin permiso específico o con membresía, empresa, tipo,
  serie o sucursal incompatibles es rechazado sin consumir un número.
- **SC-011**: El 100% de las emisiones anuladas conserva su número sin reutilización, y el 100%
  de los fallos anteriores a completar una emisión deja el consecutivo sin cambios.
- **SC-012**: El 100% de las cargas de logos con formato no permitido, tamaño superior a 2 MB,
  membresía inactiva, permiso insuficiente o contexto de otra empresa se rechaza sin modificar
  la referencia ni los archivos de logos ajenos.
- **SC-013**: El 100% de las cargas, sustituciones y eliminaciones exitosas de logos deja la
  referencia de Company consistente con el archivo almacenado (null después de eliminar) y
  contiene su registro de auditoría; las sustituciones y eliminaciones no conservan el archivo
  anterior como logo vigente.

## Assumptions

- La solución seguirá la Constitución vigente de ELANGI ERP; esta especificación describe
  comportamiento y no redefine la arquitectura ni el stack aprobados.
- Una empresa puede estar Active o Suspended. Suspender una empresa bloquea el acceso empresarial
  sin cambiar el estado individual de sus membresías; reactivarla restaura el acceso solo a
  membresías Active. Una empresa nueva inicia Suspended y requiere activación explícita del
  Platform Owner. El administrador inicial puede aceptar su invitación mientras la empresa está
  suspendida, pero no puede acceder a la empresa hasta que se active.
- La invitación vence a los siete días, es de un solo uso y solo puede aceptarla la identidad
  autenticada cuyo correo coincida con el destinatario. Una invitación vencida requiere una nueva.
- El sistema ofrece los roles base Platform Owner, Company Administrator y Company User.
  Company Administrators autorizados pueden crear roles adicionales limitados a su empresa a
  partir de un catálogo de permisos definido por el producto, que no incluye permisos para
  módulos fuera de alcance.
- Una empresa debe conservar al menos un Company Administrator activo. La revocación es terminal
  para esa membresía; para recuperar acceso se emite una nueva invitación.
- Una persona puede tener como máximo una empresa predeterminada a la vez. Solo una membresía
  Active puede ser predeterminada; si deja de estar Active, se elimina esa selección sin cambiar
  otras membresías.
- La información de suscripción se limita a referencia de plan, estado descriptivo y fechas
  informativas cuando existan; no contiene cobros, facturas, métodos de pago ni automatización de
  renovaciones.
- Las opciones regionales válidas se obtienen de catálogos de referencia mantenidos por el
  producto. La moneda principal debe estar incluida entre las monedas habilitadas; separadores
  decimal y de miles deben diferir. Las precisiones permitidas son de 0 a 8 posiciones decimales;
  la precisión monetaria se establece independientemente para cada moneda habilitada.
- Los datos relevantes de auditoría excluyen contraseñas, tokens, secretos y credenciales. Una
  operación sensible no se considera exitosa si no queda auditada.
- El logo es opcional y se almacena como objeto en `company-logos`, con lectura pública; solo el
  backend autorizado puede cargar, reemplazar o eliminar objetos. Cada ruta pertenece a una
  empresa. Company almacena únicamente `LogoStoragePath` nullable, nunca un binario ni una URL
  fija. El backend valida contenido/formato PNG, JPG/JPEG o WebP y un tamaño máximo de 2 MB.
- Los datos mínimos de empresa, sucursal y tercero incluyen nombre y datos de identificación y
  contacto apropiados para su tipo. Los requisitos legales concretos de identificación varían
  por país y quedan sujetos a especificación antes de implementar validaciones jurisdiccionales.
- `DocumentSources` es el catálogo de tipos de documento disponible para habilitar en series;
  `DocumentSeries` es una entidad empresarial diferente de `Branches`, con relación obligatoria
  de una serie a una sucursal y permitiendo varias series por sucursal. La asignación de números
  se limita al consecutivo fundacional con inicio en 1, prefijo y relleno con ceros configurables
  por combinación, y sin reinicios periódicos; no implementa los flujos operativos de facturas,
  pagos, compras u otros documentos. Una anulación posterior a la emisión conserva el número y
  no lo reutiliza; los fallos previos a completar la emisión no consumen un número.
- Las métricas porcentuales de éxito se verifican contra todos los casos de aceptación definidos
  para la característica; el objetivo de cinco minutos y 90% se valida mediante pruebas de
  usabilidad y no implica disponibilidad o rendimiento de producción.

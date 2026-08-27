# StoreControl

StoreControl é um laboratório de estudos que simula um sistema empresarial de gestão de lojas. A baseline evolui como um monólito modular com frontend React separado, API ASP.NET Core, SQL Server, Redis, RabbitMQ e Worker .NET.

## Estado atual

A fundação, segurança, lojas e os cadastros de categorias, produtos, clientes e fornecedores estão implementados. Estoque, compras, vendas, caixa, relatórios, cache e mensageria de negócio permanecem pendentes. O estado verificável está em [docs/project-state.md](docs/project-state.md).

## Stack ativa

- .NET 10, ASP.NET Core Web API, Worker e DbMigrator;
- Entity Framework Core 10, ASP.NET Core Identity e SQL Server;
- JWT curto, refresh token rotativo em cookie HttpOnly e policies por permissão;
- React, TypeScript, Vite, React Router, Axios e React Hook Form;
- Swagger/OpenAPI, Problem Details, Serilog, correlation ID e tracing OpenTelemetry inicial;
- Docker Compose com SQL Server, Redis e RabbitMQ;
- xUnit, integração com SQL Server real e GitHub Actions.

## Execução completa com Docker

Pré-requisitos: Docker Desktop ou Docker Engine com Compose.

    Copy-Item .env.example .env
    docker compose up --build

Endereços padrão:

- frontend: http://localhost:5173;
- API: http://localhost:5090;
- Swagger: http://localhost:5090/swagger;
- health: http://localhost:5090/health;
- RabbitMQ Management: http://localhost:15672.

O DbMigrator aplica migrations e seeds antes de API e Worker iniciarem. A API nunca altera o schema no startup.

## Contas locais de demonstração

São criadas apenas quando Seed__DemoDataEnabled=true. As senhas vêm do ambiente; os valores abaixo correspondem ao arquivo de exemplo:

| Perfil | E-mail | Senha local de exemplo |
| --- | --- | --- |
| Administrador | admin@storecontrol.local | StoreControl_Admin_2026! |
| Gerente | gerente@storecontrol.local | StoreControl_User_2026! |
| Vendedor | vendedor@storecontrol.local | StoreControl_User_2026! |
| Estoquista | estoquista@storecontrol.local | StoreControl_User_2026! |

Essas credenciais são deliberadamente locais e demonstrativas. Não reutilize as senhas e não versione o arquivo .env.

Com o seed demonstrativo habilitado, o migrador também garante 8 categorias, 120 produtos, 30 clientes e 12 fornecedores coerentes e idempotentes. Isso permite testar busca, filtros e paginação sem cadastro manual inicial.

## Desenvolvimento híbrido

É possível usar SQL Server local ou subir somente dependências com Compose:

    Copy-Item .env.example .env
    docker compose up -d sqlserver redis rabbitmq

Em um terminal do migrador:

    $env:ConnectionStrings__StoreControl = 'Server=localhost,1433;Database=StoreControl;User Id=sa;Password=StoreControl_Local_Only_2026!;Encrypt=False;TrustServerCertificate=True'
    $env:Seed__DemoDataEnabled = 'true'
    $env:Seed__AdminPassword = 'StoreControl_Admin_2026!'
    $env:Seed__UserPassword = 'StoreControl_User_2026!'
    dotnet run --project backend/src/StoreControl.DbMigrator

Em outro terminal da API:

    $env:ConnectionStrings__StoreControl = 'Server=localhost,1433;Database=StoreControl;User Id=sa;Password=StoreControl_Local_Only_2026!;Encrypt=False;TrustServerCertificate=True'
    $env:Authentication__Jwt__SigningKey = 'StoreControl_Local_Jwt_Signing_Key_Change_Me_2026!'
    $env:Authentication__RefreshToken__CookieSecure = 'false'
    dotnet run --project backend/src/StoreControl.Api

Frontend:

    Set-Location frontend/StoreControl.Web
    npm ci
    npm run dev

## Build e testes

Os testes de integração criam e removem um banco efêmero com prefixo StoreControl_IntegrationTests_. Informe uma conexão com permissão para criar bancos:

    $env:STORECONTROL_TEST_DB_CONNECTION = 'Server=localhost,1433;Database=master;User Id=sa;Password=StoreControl_Local_Only_2026!;Encrypt=False;TrustServerCertificate=True'
    dotnet tool restore
    dotnet restore StoreControl.sln
    dotnet build StoreControl.sln --no-restore
    dotnet test StoreControl.sln --no-build

Frontend:

    Set-Location frontend/StoreControl.Web
    npm ci
    npm run typecheck
    npm run lint
    npm run build

## Migrations

Para criar uma migration:

    $env:STORECONTROL_DB_CONNECTION = 'Server=localhost,1433;Database=StoreControl;User Id=sa;Password=StoreControl_Local_Only_2026!;Encrypt=False;TrustServerCertificate=True'
    dotnet ef migrations add NomeDaMigration --project backend/src/StoreControl.Infrastructure --startup-project backend/src/StoreControl.Infrastructure --context StoreControlDbContext --output-dir Persistence/Migrations

Para gerar um script idempotente:

    dotnet ef migrations script --idempotent --project backend/src/StoreControl.Infrastructure --startup-project backend/src/StoreControl.Infrastructure --context StoreControlDbContext

## Documentação

- [docs/specification.md](docs/specification.md): escopo canônico;
- [docs/architecture.md](docs/architecture.md): arquitetura e dependências;
- [docs/authentication.md](docs/authentication.md): autenticação, sessão e autorização;
- [docs/master-data.md](docs/master-data.md): categorias, produtos, clientes e fornecedores;
- [docs/debugging-map.md](docs/debugging-map.md): caminhos de diagnóstico;
- [docs/project-state.md](docs/project-state.md): estado, migrations e validações.

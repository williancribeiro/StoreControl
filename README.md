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

    .\scripts\Initialize-LocalEnvironment.ps1 -EnableDemoData
    docker compose up --build

O script cria um `.env` ignorado pelo Git com valores aleatórios independentes para SQL Server, Redis, RabbitMQ, JWT e usuários demonstrativos. Ele se recusa a sobrescrever um arquivo existente sem `-Force` e não imprime os segredos no terminal. O arquivo `.env.example` contém apenas nomes de variáveis e valores não secretos; copiá-lo sem preencher os campos obrigatórios faz o Compose falhar de forma explícita.

Endereços padrão da própria máquina local:

- frontend: http://localhost:5173;
- API: http://localhost:5090;
- Swagger: http://localhost:5090/swagger;
- health: http://localhost:5090/health;
- RabbitMQ Management: http://localhost:15672.

O DbMigrator aplica migrations e seeds antes de API e Worker iniciarem. A API nunca altera o schema no startup.

Este Compose é uma conveniência de desenvolvimento, não uma configuração de produção. As portas são vinculadas somente a `127.0.0.1`; não altere esse bind nem publique o ambiente na internet. Uma implantação real ainda exigiria HTTPS, gestão externa de segredos, usuário SQL de privilégio mínimo e revisão da infraestrutura descrita em [docs/security.md](docs/security.md).

## Contas locais de demonstração

São criadas somente quando `SEED_DEMO_DATA=true`, opção ativada pelo parâmetro `-EnableDemoData`. As identidades locais são:

| Perfil | E-mail |
| --- | --- |
| Administrador | admin@storecontrol.local |
| Gerente | gerente@storecontrol.local |
| Vendedor | vendedor@storecontrol.local |
| Estoquista | estoquista@storecontrol.local |

As senhas aleatórias ficam exclusivamente no `.env` local, nas variáveis `DEMO_ADMIN_PASSWORD` e `DEMO_USER_PASSWORD`. Não reutilize, compartilhe ou versione esse arquivo. Os endereços `.local` não representam contas reais.

Com o seed demonstrativo habilitado, o migrador também garante 8 categorias, 120 produtos, 30 clientes e 12 fornecedores coerentes e idempotentes. Isso permite testar busca, filtros e paginação sem cadastro manual inicial.

## Desenvolvimento híbrido

É possível usar SQL Server local ou subir somente dependências com Compose:

    .\scripts\Initialize-LocalEnvironment.ps1 -EnableDemoData
    docker compose up -d sqlserver redis rabbitmq

Execute o carregador em cada terminal que iniciará uma aplicação .NET; ele não exibe os valores:

    .\scripts\Set-LocalEnvironment.ps1

Em um terminal do migrador:

    dotnet run --project backend/src/StoreControl.DbMigrator

Em outro terminal da API:

    .\scripts\Set-LocalEnvironment.ps1
    dotnet run --project backend/src/StoreControl.Api

Frontend:

    Set-Location frontend/StoreControl.Web
    npm ci
    npm run dev

## Build e testes

Os testes de integração criam e removem um banco efêmero com prefixo StoreControl_IntegrationTests_. Informe uma conexão com permissão para criar bancos:

    .\scripts\Set-LocalEnvironment.ps1
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

    .\scripts\Set-LocalEnvironment.ps1
    dotnet ef migrations add NomeDaMigration --project backend/src/StoreControl.Infrastructure --startup-project backend/src/StoreControl.Infrastructure --context StoreControlDbContext --output-dir Persistence/Migrations

Para gerar um script idempotente:

    dotnet ef migrations script --idempotent --project backend/src/StoreControl.Infrastructure --startup-project backend/src/StoreControl.Infrastructure --context StoreControlDbContext

## Documentação

- [docs/specification.md](docs/specification.md): escopo canônico;
- [docs/architecture.md](docs/architecture.md): arquitetura e dependências;
- [docs/authentication.md](docs/authentication.md): autenticação, sessão e autorização;
- [docs/master-data.md](docs/master-data.md): categorias, produtos, clientes e fornecedores;
- [docs/debugging-map.md](docs/debugging-map.md): caminhos de diagnóstico;
- [docs/security.md](docs/security.md): limites de segurança e configuração local;
- [docs/project-state.md](docs/project-state.md): estado, migrations e validações.

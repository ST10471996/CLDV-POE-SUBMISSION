# CoffeeNChill — Canteen Management System (Cloud PoE: Parts 1, 2 & 3)

A cloud-enabled microservices system that replaces CoffeeNChill's paper menus, handwritten order slips,
filing-cabinet recipe documents and locally saved promo photos.

**One repository, one stack, one command.** Parts 1 and 2 are not separate projects — the Part 1
Azure Functions app was extended in Part 2 with queue triggers, and in Part 3 it was placed behind a
JWT-secured ASP.NET Core gateway. The single `docker compose up -d` in the root starts all three parts
together.

| Part | What it adds | Component |
|---|---|---|
| 1 (100) | Menu in Azure Table Storage, staff documents in an Azure File Share, standalone Docker containers | `src/CoffeeNChill.Functions` |
| 2 (100) | Asynchronous order queue + queue-triggered processor, Docker Compose from Docker Hub | same app (extended) + `docker-compose.yml` |
| 3 (100 + 10 bonus) | API gateway, JWT auth, RBAC, live VPS deployment, CI/CD pipeline | `src/CoffeeNChill.Gateway`, `.github/workflows/deploy.yml` |

---

## 1. Architecture

```
            Public (students)                    Staff / Admin (JWT Bearer)
                    |                                      |
                    +--------------+-----------------------+
                                   v
                    +-----------------------------------+
                    |   CoffeeNChill.Gateway  (.NET 8)  |
                    |   ASP.NET Core Web API + Swagger   |
                    |   JWT issue/validate, RBAC         |
                    |   Users table via Azure SDK        |
                    +-----------------------------------+
                       |                          |
             HTTP (server-to-server)      Azure Tables SDK
                       v                          v
        +----------------------------+    +------------------+
        | CoffeeNChill.Functions     |    |  Users table     |
        | MenuItems | staff-docs     |    |  (Admin/Staff/   |
        | orders    | Orders table   |    |   Customer)      |
        | order-processing-queue     |    +------------------+
        +----------------------------+
                       |
                       v
        +-----------------------------------------------+
        | Azurite container: Tables(10002) Queues(10001) |
        | Blobs(10000)   +  Azure File Share (staff-docs)|
        +-----------------------------------------------+
```

The gateway does two things: it stores and checks user profiles itself, and it forwards every
menu/document/order call to the Functions host. That is why there is exactly one place that decides
who may do what, and exactly one gap for a request to cross.

---

## 2. Project layout

```
CoffeeNChill/
├── CoffeeNChill.sln
├── docker-compose.yml              # Part 2 + Part 3: whole stack, images from Docker Hub
├── .env.example                    # copy to .env and fill in
├── src/
│   ├── CoffeeNChill.Functions/     # Parts 1 & 2 — Azure Functions (isolated, .NET 8)
│   │   ├── Functions/MenuFunctions.cs        # CreateMenuItem, GetAllMenuItems,
│   │   │                                     # GetMenuItemsByCategory, UpdateMenuItem, DeleteMenuItem
│   │   ├── Functions/DocumentFunctions.cs    # Upload/List/Download/Delete staff documents
│   │   ├── Functions/OrderFunctions.cs       # QueueOrder, ProcessOrderQueue, order status
│   │   ├── Models/Models.cs                  # Table entities, DTOs, queue contract
│   │   ├── Program.cs  host.json  local.settings.json
│   │   └── Dockerfile
│   └── CoffeeNChill.Gateway/       # Part 3 — ASP.NET Core Web API
│       ├── Controllers/  AuthController, MenuController, DocumentsController, OrdersController
│       ├── Services/     AuthServices (Users table, BCrypt, JWT), FunctionsClient
│       ├── Dtos/Dtos.cs  appsettings.json  Dockerfile
├── tests/CoffeeNChill.Tests/       # xUnit tests run by the CI/CD pipeline
├── docker/azurite/Dockerfile       # Azurite re-tagged for Docker Hub
├── docs/                           # Postman collection, environment, sample staff documents
├── scripts/                        # vps-setup.sh, build-and-push.sh, part1-standalone.sh
└── .github/workflows/deploy.yml    # BONUS: build → test → push → deploy over SSH
```

---

## 3. Data model

**MenuItems table** — PartitionKey `Category` ("Hot Drinks", "Cold Drinks", "Pastries", "Sandwiches"),
RowKey `Sku` (`COF-001`, `PAS-104`), plus `Name`, `Description`, `Price`, `IsAvailable`.

**staff-docs file share** — barista recipe sheets, equipment cleaning manuals, health & safety policies.
`ListStaffDocuments` returns file name, size and last modified date for each one.

**order-processing-queue** — one JSON message per placed order:

```json
{
  "OrderId": "ORD-2026-8801",
  "CustomerName": "Jane Smith",
  "SelectedItemSKUs": ["COF-001", "PAS-104"],
  "TotalPrice": 65.00,
  "OrderTimestamp": "2026-10-12T10:15:00Z"
}
```

**Orders table** — PartitionKey `OrderDate` (`yyyy-MM-dd`), RowKey `OrderId`, plus `CustomerName`,
`SelectedItemSKUs`, `TotalPrice`, `OrderTimestamp`, `Status`. `ProcessOrderQueue` writes the row as
`Received` and then walks it through `Preparing → Ready → Collected`.

**Users table** — PartitionKey `Role` (`Admin` | `Staff` | `Customer`), RowKey email,
`PasswordHash` (BCrypt, never plain text), `FullName`, `CreatedDate`.

---

## 4. Endpoints and who may call them

| Method & route | Access | Purpose |
|---|---|---|
| `POST /api/auth/register` | anonymous | Create a user (anonymous callers always become `Customer`) |
| `POST /api/auth/login` | anonymous | Validate credentials, return a signed JWT |
| `GET /api/auth/me` | any token | Show the claims in the calling token |
| `GET /api/auth/users` | Admin | List users |
| `GET /api/menu` | **anonymous** | Full menu |
| `GET /api/menu/category/{category}` | **anonymous** | Menu by category |
| `POST /api/menu` | Admin, Staff | Add a menu item |
| `PUT /api/menu/{category}/{id}` | Admin, Staff | Change price / availability |
| `DELETE /api/menu/{category}/{id}` | Admin, Staff | Remove an item |
| `GET /api/documents` | Admin, Staff | List staff documents |
| `GET /api/documents/download/{fileName}` | Admin, Staff | Download a document |
| `POST /api/documents/upload` | Admin, Staff | Upload (multipart/form-data) |
| `DELETE /api/documents/{fileName}` | Admin, Staff | Delete a document |
| `POST /api/orders/queue` | **anonymous** | Place an order (returns `202` immediately) |
| `GET /api/orders/{orderDate}/{orderId}` | **anonymous** | Track one order |
| `GET /api/orders` | Admin, Staff | List orders (optional `?date=`) |
| `PUT /api/orders/status` | Admin, Staff | Move an order's status |

The same operations exist on the Functions host directly (`http://localhost:7071/api/...`) for the
Part 1/Part 2 demonstrations; the gateway simply secures them for public traffic.

---

## 5. Running it locally

### Option A — the whole stack (Parts 1–3) with one command

```bash
cp .env.example .env          # set DOCKERHUB_USERNAME, JWT_KEY
docker compose up -d
docker compose ps
```

Then:

* Swagger UI — http://localhost:5000/swagger
* Functions host — http://localhost:7071/api/menu
* Azurite — ports 10000 (blob), 10001 (queue), 10002 (table)

Stop with `docker compose down` (add `-v` to wipe the Azurite volume).

### Option B — Part 1 as standalone containers (no Compose)

```bash
docker network create cnc-net
docker run -d --name azurite --network cnc-net -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  <dockerhub_username>/coffeenchill-azurite:v1.0
docker run -d --name functions --network cnc-net -p 7071:80 \
  -e AzureWebJobsStorage="UseDevelopmentStorage=true" \
  <dockerhub_username>/coffeenchill-functions:v1.0
```

or just run `./scripts/part1-standalone.sh <dockerhub_username>`.

### Option C — from source, no Docker

```bash
# terminal 1 — storage
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite

# terminal 2 — functions
dotnet run --project src/CoffeeNChill.Functions        # http://localhost:7071

# terminal 3 — gateway (Swagger on http://localhost:5000/swagger)
dotnet run --project src/CoffeeNChill.Gateway

# tests
dotnet test
```

### Building and pushing the images

```bash
./scripts/build-and-push.sh <dockerhub_username> v3.0
```

Tags used across the PoE: `coffeenchill-functions:v1.0` and `coffeenchill-azurite:v1.0` (Part 1),
`coffeenchill-functions:v2.0` (Part 2), `v3.0` for the final Part 3 stack.

### Azure Files note

Azurite emulates Tables, Queues and Blobs, but **not Azure Files**. The menu and order features run
fully on Azurite; for the `staff-docs` file share, set `FILES_CONNECTION` in `.env` to a free-tier
Azure Storage account connection string (the Functions app reads it from `FilesStorage` and falls
back to `AzureWebJobsStorage` when it is empty). Everything else stays local.

---

## 6. Deploying to the VPS (Part 3)

```bash
# 1. Point your DNS or note your droplet/instance IP, then on the VPS:
./scripts/vps-setup.sh                 # installs Docker Engine + Compose, opens port 5000

# 2. Copy docker-compose.yml and .env into ~/coffeenchill, then:
cd ~/coffeenchill && docker compose up -d

# 3. Public application URL:
#    http://<VPS-IP>:5000/swagger
```

No AI-generated voices are permitted in the demonstration videos, and the video link must be in this
README (see the placeholders below).

---

## 7. Continuous integration / deployment (bonus 10 marks)

`.github/workflows/deploy.yml` runs on every push to `main`:

1. Check out the code and install the .NET 8 SDK.
2. Build the solution and run the xUnit tests.
3. Build the Docker images.
4. Push version-tagged images (`v3.<run number>`) to Docker Hub.
5. Copy `docker-compose.yml` to the VPS over SCP, then SSH in and run `docker compose pull && docker compose up -d`.

Repository secrets to add (Settings → Secrets and variables → Actions):

| Secret | Value |
|---|---|
| `DOCKERHUB_USERNAME` | your Docker Hub user |
| `DOCKERHUB_TOKEN` | Docker Hub access token |
| `VPS_HOST` | VPS IP or hostname |
| `VPS_USER` | SSH user |
| `VPS_SSH_KEY` | private key authorised on the VPS |

---

## 8. Test with the Postman collection

`docs/CoffeeNChill.postman_collection.json` (plus `docs/CoffeeNChill-VPS.postman_environment.json`).

Run it top to bottom with the Collection Runner. The requests set `{{orderId}}`, `{{orderDate}}`,
`{{token}}`, `{{adminToken}}`, `{{staffToken}}` and `{{customerToken}}` in pre-request/test scripts,
so nothing has to be copied by hand. Folders:

1. **Part 1 & 2 — Azure Functions direct (port 7071):** menu CRUD (including `409` on a duplicate SKU
   and `400` on a bad body), document upload/list/download, placing an order and watching the status
   change in the Orders table.
2. **Part 3 — API Gateway (port 5000):** all three logins, register (including proof an anonymous
   caller cannot self-assign `Admin`), public menu/order calls with no token, `401` without a token,
   `403` as a Customer, and the same staff actions succeeding with a Bearer `{{token}}`.

Attach a sample document from `docs/sample-docs/` when the collection asks for a file.

---

## 9. Changelog

**Part 3 — `v3.0`**

* Added `CoffeeNChill.Gateway`: ASP.NET Core Web API gateway with Swagger/OpenAPI, XML documentation on endpoints, request bodies and response codes, and a DTO layer.
* Added **JWT authentication**: `POST /api/auth/register` hashes passwords with BCrypt into the `Users` table; `POST /api/auth/login` issues a token carrying `ClaimTypes.Name`, `ClaimTypes.Email` and `ClaimTypes.Role`.
* Added **role-based authorisation**: menu browsing and order placement stay anonymous; menu changes, documents and order status require `Admin,Staff`.
* Added `docker-compose.yml` service `gateway` (port 5000) and its Dockerfile; all services still pull from Docker Hub.
* Added `deploy.yml` GitHub Actions pipeline (build → test → push → SSH deploy) for the bonus marks.
* Added unit tests for password hashing, JWT claims and role normalisation.
* Added `scripts/vps-setup.sh` for provisioning the Linux VPS.

**Part 2 — `v2.0`**

* Added the `order-processing-queue` producer endpoint (`POST /api/orders/queue`) with sample payload.
* Added the `ProcessOrderQueue` queue-triggered function and the `Orders` table.
* Added the barista lifecycle simulation `Received → Preparing → Ready → Collected`.
* Added `docker-compose.yml` orchestrating Azurite and the Functions host from Docker Hub images.
* Extended the Postman collection with queue and status checks.

**Part 1 — `v1.0`**

* Created the `MenuItems` table and the five menu HTTP functions.
* Created the `staff-docs` file share and the upload/list/download document functions.
* Added the Functions Dockerfile, the Azurite image and standalone `docker run` instructions.
* Published `coffeenchill-functions:v1.0` and `coffeenchill-azurite:v1.0` to Docker Hub.

---

## 10. Team contributions

Replace each row with your own details before submitting — the marker checks commit history against
this table, and each member needs **at least five meaningful commits per part**.

| Team member (name + student number) | GitHub handle | Part 1 | Part 2 | Part 3 |
|---|---|---|---|---|
| _Member 1_ | _@handle_ | _e.g. MenuItems table + menu functions_ | _queue producer endpoint_ | _JWT service + auth endpoints_ |
| _Member 2_ | _@handle_ | _staff-docs file share + document functions_ | _ProcessOrderQueue function_ | _gateway proxy controllers + RBAC policies_ |
| _Member 3_ | _@handle_ | _Dockerfile + Azurite image + Docker Hub push_ | _docker-compose.yml from Docker Hub images_ | _VPS provisioning + README/Swagger docs_ |
| _Member 4_ | _@handle_ | _Postman collection + tests_ | _queue and status Postman tests_ | _Postman auth/Bearer tests + unit tests_ |

## 11. Submission links

| Item | Link |
|---|---|
| GitHub repository | _https://github.com/<user>/CoffeeNChill_ |
| Docker Hub — functions image | _https://hub.docker.com/r/<user>/coffeenchill-functions_ |
| Docker Hub — gateway image | _https://hub.docker.com/r/<user>/coffeenchill-gateway_ |
| Docker Hub — Azurite image | _https://hub.docker.com/r/<user>/coffeenchill-azurite_ |
| VPS application URL | _http://<VPS-IP>:5000/swagger_ |
| Part 1 video (unlisted YouTube) | _https://youtu.be/<id>_ |
| Part 2 video (unlisted YouTube) | _https://youtu.be/<id>_ |
| Part 3 video (unlisted YouTube) | _https://youtu.be/<id>_ |

## 12. Troubleshooting

| Symptom | Fix |
|---|---|
| `Jwt:Key must be at least 32 characters` | Set a longer `JWT_KEY` in `.env` (or `Jwt__Key`). |
| Gateway returns `502 Functions host unavailable` | The Functions container is not up yet, or `Functions__BaseUrl` is wrong. Check `docker compose logs functions`. |
| Menu calls succeed but documents fail | `FILES_CONNECTION` is not set — Azurite cannot emulate Azure Files. See the note in section 5. |
| Order stays `Received` | `SimulateLifecycle=false`; staff drive it with `PUT /api/orders/status`, or set it back to `true`. |
| Postman `403` where you expected `200` | You are using the Customer token; the staff requests need `{{staffToken}}` in `{{token}}`. |
| Tables look empty after `docker compose down -v` | That flag deletes the Azurite volume — expected. |

# 🔐 SecureTrace — Cryptographically Linked Digital Evidence Locker

> A Service-Oriented platform for secure forensic evidence management, built with .NET 10 and React.

![Build Status](https://github.com/shankure/blockchain/actions/workflows/deploy.yml/badge.svg)

> **Note:** The live deployment URLs were active during the presentation period. 
> The services have been undeployed after the project demonstration.

---

## 📋 Table of Contents

- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Technologies Used](#technologies-used)
- [Role-Based Access Control](#role-based-access-control)
- [API Endpoints](#api-endpoints)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Running the Tests](#running-the-tests)
- [Cryptographic Chain — How It Works](#cryptographic-chain--how-it-works)
- [Environment Variables](#environment-variables)
- [CI/CD Pipeline](#cicd-pipeline)

---

## Project Overview

SecureTrace addresses the **Chain of Custody** problem in legal and forensic environments. When digital evidence is collected, it must be provably unmodified from the moment of collection to the moment it is presented in court.

SecureTrace solves this by implementing a **Linked Hash Ledger** — every time evidence is created or updated, a new cryptographic block is appended to an append-only MongoDB collection. Each block contains a SHA-256 hash of the previous block, making unauthorized data alteration mathematically detectable.

**Core Features:**
- Case and Evidence Management (full CRUD)
- Cryptographic Integrity Verification (SHA-256 chain)
- Role-Based Access Control (Admin, User, Auditor)
- Automated Chain Verification Engine
- React frontend with real-time ledger visualization

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   React Frontend                     │
│         (Vite + React Router + Axios)               │
└───────────────────────┬─────────────────────────────┘
                        │ HTTPS + JWT Bearer Token
┌───────────────────────▼─────────────────────────────┐
│              ASP.NET Core Web API (.NET 10)          │
│                                                      │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────┐  │
│  │ Auth        │  │ Cases        │  │ Evidence   │  │
│  │ Controller  │  │ Controller   │  │ Controller │  │
│  └──────┬──────┘  └──────┬───────┘  └─────┬──────┘  │
│         │                │                 │         │
│  ┌──────▼──────┐  ┌──────▼───────┐  ┌─────▼──────┐  │
│  │ AuthService │  │ ICaseRepo    │  │IEvidenceRep│  │
│  │ JwtService  │  │ CaseRepo     │  │EvidenceRepo│  │
│  └─────────────┘  └──────┬───────┘  └─────┬──────┘  │
│                           │                │         │
│                    ┌──────▼────────────────▼──────┐  │
│                    │     AuditService              │  │
│                    │  CryptographyService          │  │
│                    │  VerificationService          │  │
│                    └──────┬───────────────┬───────┘  │
└───────────────────────────┼───────────────┼──────────┘
                            │               │
               ┌────────────▼───┐   ┌───────▼────────┐
               │  PostgreSQL    │   │    MongoDB      │
               │  (EF Core)     │   │  Audit Ledger   │
               │                │   │  (append-only)  │
               │  Users         │   │  audit_blocks   │
               │  Cases         │   └────────────────┘
               │  Evidences     │
               └────────────────┘
```

---

## Technologies Used

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core 9 |
| Relational DB | PostgreSQL (via Npgsql) |
| Audit Ledger | MongoDB (append-only collection) |
| Authentication | JWT Bearer Tokens |
| Password Hashing | BCrypt.Net |
| Cryptography | SHA-256 (`System.Security.Cryptography`) |
| Frontend | React 19 + Vite |
| HTTP Client | Axios |
| Routing | React Router v7 |
| Unit Testing | xUnit + Moq + FluentAssertions |
| CI/CD | GitHub Actions |
| Cloud | Azure App Service |

---

## Role-Based Access Control

SecureTrace implements three distinct roles, each representing a real-world actor in a forensic investigation:

### 👮 Admin (Lead Investigator)
Full system access. Responsible for opening cases, managing users, and archiving evidence.
- Create, update, delete Cases
- Upload, update, delete Evidence
- Run chain verification

### 🕵️ User (Field Agent)
Can contribute evidence but cannot manage case structure.
- View all cases and evidence
- Upload and update Evidence
- Run chain verification
- ❌ Cannot create, update, or delete Cases
- ❌ Cannot delete Evidence

### ⚖️ Auditor (Legal Auditor)
Read-only access. Exists to verify chain integrity for court proceedings.
- View all cases and evidence
- Run chain verification (`GET /api/audit/verify`)
- View all audit blocks (`GET /api/audit/blocks`)
- ❌ Cannot create or modify anything

---

## API Endpoints

### Authentication
| Method | Endpoint | Access | Description |
|--------|----------|--------|-------------|
| POST | `/api/auth/register` | Public | Register a new user |
| POST | `/api/auth/login` | Public | Login and receive JWT token |

### Cases
| Method | Endpoint | Access | Description |
|--------|----------|--------|-------------|
| GET | `/api/cases` | All roles | Get all cases |
| GET | `/api/cases/{id}` | All roles | Get case by ID |
| POST | `/api/cases` | Admin only | Create a new case |
| PUT | `/api/cases/{id}` | Admin only | Update a case |
| DELETE | `/api/cases/{id}` | Admin only | Delete a case |

### Evidence
| Method | Endpoint | Access | Description |
|--------|----------|--------|-------------|
| GET | `/api/evidence` | All roles | Get all evidence |
| GET | `/api/evidence/{id}` | All roles | Get evidence by ID |
| GET | `/api/evidence/case/{caseId}` | All roles | Get evidence by case |
| POST | `/api/evidence` | Admin, User | Upload new evidence (triggers audit block) |
| PUT | `/api/evidence/{id}` | Admin, User | Update evidence (triggers audit block) |
| DELETE | `/api/evidence/{id}` | Admin only | Delete evidence |

### Audit
| Method | Endpoint | Access | Description |
|--------|----------|--------|-------------|
| GET | `/api/audit/verify` | All roles | Verify entire chain integrity |
| GET | `/api/audit/blocks` | All roles | List all audit blocks |

---

## Project Structure

```
blockchain/
├── .github/
│   └── workflows/
│       └── deploy.yml          # GitHub Actions CI/CD pipeline
├── code/
│   ├── SecureTrace.API/
│   │   ├── Controllers/        # API controllers (Auth, Cases, Evidence, Audit)
│   │   ├── Data/               # EF Core DbContext + MongoDB context
│   │   ├── DTOs/               # Request/Response data transfer objects
│   │   ├── Middleware/         # Custom middleware
│   │   ├── Models/             # Entity models (User, Case, Evidence, AuditBlock)
│   │   ├── Repositories/       # ICaseRepository, IEvidenceRepository + implementations
│   │   ├── Services/           # Business logic (Auth, JWT, Audit, Crypto, Verification)
│   │   ├── appsettings.json    # Base configuration (no secrets)
│   │   └── Program.cs          # Application entry point and DI setup
│   ├── SecureTrace.Tests/
│   │   ├── AuthServiceTests.cs         # Auth business logic tests
│   │   ├── CaseRepositoryTests.cs      # Repository layer tests
│   │   ├── CasesControllerTests.cs     # Controller layer tests
│   │   ├── CryptographyServiceTests.cs # SHA-256 hashing tests
│   │   └── VerificationServiceTests.cs # Chain verification tests
│   └── securetrace-ui/
│       └── src/
│           ├── api/            # Axios instance with JWT interceptor
│           ├── components/     # Navbar
│           ├── context/        # AuthContext (global auth state)
│           └── pages/          # Login, Dashboard, Evidence, AuditLedger
├── paper/                      # SEEU project paper
├── slides/                     # Presentation slides
└── video/                      # Application walkthrough video
```

---

## Prerequisites

Make sure you have the following installed:

| Tool | Version | Download |
|---|---|---|
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Node.js | 18+ | [nodejs.org](https://nodejs.org) |
| PostgreSQL | 15+ | [postgresql.org](https://www.postgresql.org/download/) |
| MongoDB | 7+ | [mongodb.com](https://www.mongodb.com/try/download/community) |
| MongoDB Compass | Latest | Bundled with MongoDB installer |

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/shankure/blockchain.git
cd blockchain
```

### 2. Configure the API

The API reads secrets from .NET User Secrets (locally) or Azure App Settings (in production). Set up your local secrets:

```bash
cd code/SecureTrace.API

dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=SecureTraceDb;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "MongoDB:DatabaseName" "SecureTraceAudit"
dotnet user-secrets set "Jwt:Key" "YourSuperSecretKeyThatIsAtLeast32Characters"
dotnet user-secrets set "Jwt:Issuer" "SecureTrace.API"
dotnet user-secrets set "Jwt:Audience" "SecureTrace.Client"
dotnet user-secrets set "Jwt:ExpiresInMinutes" "60"
```

### 3. Run the API

```bash
cd code/SecureTrace.API
dotnet restore
dotnet run
```

The API will:
- Automatically apply EF Core migrations and create the PostgreSQL database
- Start listening on `https://localhost:63094`
- Serve Swagger UI at `https://localhost:63094/swagger`

### 4. Run the Frontend

Open a second terminal:

```bash
cd code/securetrace-ui
npm install
npm run dev
```

The React app starts at `http://localhost:5173`

### 5. Register your first user

Open Swagger at `https://localhost:63094/swagger` and call:

```
POST /api/auth/register
{
  "fullName": "Lead Investigator",
  "email": "admin@securetrace.com",
  "password": "YourPassword123!",
  "role": "Admin"
}
```

---

## Running the Tests

```bash
cd code/SecureTrace.Tests
dotnet test --verbosity normal
```

Expected output:
```
Test summary: total: 30, failed: 0, succeeded: 30, skipped: 0
```

**Test coverage:**
- `CryptographyServiceTests` — SHA-256 hashing, avalanche effect, payload building
- `VerificationServiceTests` — valid chain, tampered data, broken links, empty ledger
- `AuthServiceTests` — register, login, duplicate email, invalid role, wrong password
- `CaseRepositoryTests` — CRUD operations, auto case number generation
- `CasesControllerTests` — HTTP response codes, role enforcement

---

## Cryptographic Chain — How It Works

Every time evidence is created or updated, a new `AuditBlock` is appended to MongoDB:

```
Block 1 (Genesis)
├── BlockIndex:    1
├── PreviousHash:  "0000000000000000"   ← conventional genesis marker
├── CurrentHash:   SHA256(payload)
└── Payload:       "1|timestamp|evidenceId|CREATED|snapshot|actor|0000000000000000"

Block 2
├── BlockIndex:    2
├── PreviousHash:  Block1.CurrentHash   ← chain link
├── CurrentHash:   SHA256(payload)
└── Payload:       "2|timestamp|evidenceId|CREATED|snapshot|actor|Block1.CurrentHash"
```

**Verification process** (`GET /api/audit/verify`):
1. Load all blocks ordered by `BlockIndex` ascending
2. For each block, re-compute `SHA256(payload)` from stored fields
3. Compare re-computed hash with stored `CurrentHash` — mismatch = data altered
4. Compare `PreviousHash` with previous block's `CurrentHash` — mismatch = chain broken
5. Return `isValid: true` if all checks pass, `isValid: false` with exact failure reason if not

**Tamper detection:** If anyone edits a block's data directly in MongoDB without updating the hash, the verification engine will detect it immediately and report exactly which block was compromised.

---

## Environment Variables

| Key | Description | Example |
|---|---|---|
| `ConnectionStrings:Postgres` | PostgreSQL connection string | `Host=localhost;Port=5432;...` |
| `MongoDB:ConnectionString` | MongoDB connection string | `mongodb://localhost:27017` |
| `MongoDB:DatabaseName` | MongoDB database name | `SecureTraceAudit` |
| `Jwt:Key` | JWT signing key (32+ characters) | `YourSuperSecretKey...` |
| `Jwt:Issuer` | JWT issuer | `SecureTrace.API` |
| `Jwt:Audience` | JWT audience | `SecureTrace.Client` |
| `Jwt:ExpiresInMinutes` | Token expiry | `60` |

> ⚠️ Never commit real secrets to Git. Use .NET User Secrets locally and Azure App Settings in production.

---

## CI/CD Pipeline

Every push to `main` triggers the GitHub Actions pipeline:

```
Push to main
     ↓
Restore packages
     ↓
Build (Release)
     ↓
Run 30 unit tests
     ↓
Tests pass? → Deploy to Azure App Service
Tests fail? → Pipeline stops, no deployment
```

View pipeline status: [GitHub Actions](https://github.com/shankure/blockchain/actions)

---

## Academic Context

**Course:** Service Oriented Architecture  
**University:** South East European University  
**Academic Year:** 2025/2026  

**Team Members:**
- Darko Koprivnjak, dk131023

---

*SecureTrace — Because evidence integrity is not optional.*

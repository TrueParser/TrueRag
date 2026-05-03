# System Architecture

TrueRAG is designed as a **Modular Monolith** built on .NET 10+. It emphasizes logical separation of concerns while maintaining the operational simplicity of a single deployable unit.

## High-Level Design

The system is split into two primary bounded contexts within the monolith:
1. **Core Ingestion & Search Monolith:** Highly optimized layer dealing directly with CrateDB and NATS.
2. **Conversation & LLM Module:** Orchestration layer handling chat threads, memory, and LLM communication.

```mermaid
graph TD
    Client[Client Application / UI] --> Host[TrueRAG Host (Single Binary Composition Root)]
    
    subgraph TrueRAG Modular Monolith
        Host --> ApiMod[TrueRAG API Module (Stateless)]
        Host --> ConvMod[Conversation & Retrieval Module]
        Host --> IngestMod[Ingestion Module]
        
        IngestMod --> |Publishes Event| NATS[NATS Message Broker]
        NATS --> |Consumes Event (Async)| WorkerMod[Background Ingestion Workers]
    end
    
    ConvMod <--> |Prompts/Responses| LLM[LLM Provider]
    ConvMod <--> |Caching & Thread State| Redis[(Redis)]
    
    WorkerMod --> |Writes/Indexes| CentralDb[(CrateDB Central Publisher)]
    ConvMod --> |Reads/Searches| EdgeDb[(CrateDB Edge Subscriber)]
    
    subgraph CrateDB Replication
        CentralDb <--> |Logical Replication| EdgeDb
    end
```

## Core Infrastructure

1. **Host Tier (Single Binary):** `TrueRag.Host` is the single executable composition root. It owns configuration (`appsettings*.json`), module startup, DI composition, and host-level middleware.
2. **API Tier (Stateless):** The TrueRAG API module holds zero local state. All API nodes can be horizontally scaled and destroyed at will.
3. **Redis (State & Caching):** Used by the Retrieval API to cache frequent queries, manage rate-limiting, and temporarily hold active conversation thread state.
4. **NATS (Async Messaging):** When an ingestion request hits the API module, it is immediately placed on a NATS message queue. Background workers asynchronously process the heavy TrueParser JSON extraction and index the data into CrateDB.
5. **CrateDB (Storage):** Uses Logical Replication. The NATS background workers write to the Central Publisher. The Stateless API reads from the Edge Subscribers.

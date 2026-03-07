# 🔨 Distributed Real-Time Auction Engine

A high-performance, horizontally scalable bidding platform designed to showcase advanced distributed systems architecture. This project demonstrates real-time state synchronization across multiple decoupled server instances using a Redis backplane and distributed concurrency control.



## 🚀 Architectural Features

* **Redis Backplane:** Utilizes Redis Pub/Sub to synchronize SignalR messages across multiple containerized backend nodes. This ensures that a bid placed on one server is broadcasted instantaneously to all clients connected to any other server in the cluster.
* **Distributed Locking (RedLock):** Implements the RedLock algorithm to ensure atomic bid processing. This prevents race conditions in a multi-server environment, guaranteeing that only one valid bid can be processed at a single millisecond for any given auction.
* **Service Orchestration & Health Checks:** Managed via Docker Compose with integrated health checks. The backend containers wait until the Redis infrastructure is fully healthy before initializing, ensuring a reliable system startup.
* **Strongly-Typed SignalR Hubs:** Uses `Hub<IAuctionNotificationClient>` to enforce a strict contract between the server and the frontend, eliminating runtime errors and ensuring type-safety.

## 🌐 Horizontal Scalability & Physical Expansion

This system is architected for seamless horizontal scaling beyond a single machine. While the provided configuration runs on a local Docker cluster, the architecture can be extended to **multiple physical servers**:

1.  **Shared State:** By pointing the connection strings of multiple logical .NET servers—located on different physical machines—to a single, central Redis instance, the entire network acts as a unified hub.
2.  **Cross-Server Communication:** The SignalR Redis backplane handles the complexity of routing messages between physical nodes, ensuring a user on *Server A* and a user on *Server B* experience zero-latency synchronization.
3.  **Global Concurrency:** Because the RedLock implementation relies on the central Redis server, transactional integrity is maintained across the entire physical network, preventing duplicate "winning" bids regardless of which server receives the request.



## 🛠️ Technical Stack

* **Backend:** .NET Core, SignalR, Redis
* **Concurrency:** RedLock.net (Distributed Locking)
* **Infrastructure:** Docker, Docker Compose
* **Frontend:** Native HTML5, CSS3, JavaScript (Vanilla implementation)

---

## ⚙️ Setup and Execution Guide

This project is fully containerized to ensure consistent behavior across different environments. Follow these steps to build and test the distributed cluster locally.

### 1. Required Files
Ensure you have the following core files in your project directory:
* `Dockerfile` (Backend service definition)
* `docker-compose.yml` (Orchestration logic)
* `index.html`, `style.css`, `app.js` (Frontend application)

### 2. Build and Launch
Open your terminal and follow the steps below. The commands show how to pull only the repo files required to run the cluster (the `Dockerfile`, `docker-compose.yml` and the frontend files), change into the pulled directory, then build and run the Docker Compose stack.

```bash
# clone the full repository into a local directory and enter it
git clone https://github.com/HossamZaki-git/Auction.git auction-deploy
cd auction-deploy
```

Build and run the Docker Compose stack (choose the command supported by your Docker):

```bash
# modern Docker CLI
docker compose up --build

# legacy CLI
# docker-compose up --build
```

### 3. Open multiple frontend instances and connect to different servers
To observe cross-server real-time synchronization, open multiple instances of the frontend (index.html) and connect some to server1 and others to server2, bid and see the updates reflecting instantly across all clients from the two servers.

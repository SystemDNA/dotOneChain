# dotOneChain (.NET 8, MongoDB) — ERC-1155-style with Pluggable Storage (IPFS or Mongo GridFS)

dotOneChain is a production-style **1155-like** NFT API:
- **MongoDB** data model: tokens, holdings (balances), transactions, blocks.
- **Pluggable storage**: **IPFS Kubo** or **Mongo GridFS** (configure via `Storage__Mode`).
- **Wallet generation**, token creation, mint / transfer / burn (with quantity).
- ECDSA P-256 signatures with canonical messages (include `qty`).
- Docker Compose for API + MongoDB + optional IPFS.

## Quick Start
```bash
docker compose up -d --build
# Swagger: http://localhost:8080/swagger
```

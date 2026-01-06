# Game Configuration System Design

This document outlines the architecture for the Feishu-based configuration system implemented in this project.

## Overview

The system allows game designers to edit configuration data (items, levels, balance) in **Feishu (Lark) Spreadsheets** online. A build tool fetches this data, converts it into an optimized **Binary Format**, and the game runtime loads this binary file efficiently.

### Workflow

1.  **Design**: Edit data in Feishu (Collaborative, Versioned, AI-assisted).
2.  **Build**: Run `Match3.ConfigTool` to fetch JSON and compile to `config.bin`.
3.  **Runtime**: Game loads `config.bin` at startup into memory-efficient structs.

## Components

### 1. Match3.ConfigTool (Build Pipeline)
*   **Location**: `src/Match3.ConfigTool`
*   **Type**: Console Application
*   **Responsibilities**:
    *   Authenticate with Feishu Open Platform (Mocked for now).
    *   Fetch spreadsheet data as JSON.
    *   Validate data against the schema.
    *   Serialize data into a custom binary format.
*   **Output**: `config.bin`

### 2. Runtime Config (Game Core)
*   **Location**: `src/Match3.Core/Config`
*   **Components**:
    *   `ItemConfig`: Struct definition of the data.
    *   `ConfigManager`: Handles binary reading and provides API (`GetItem(id)`).
*   **Efficiency**:
    *   Uses `BinaryReader` for fast sequential reading.
    *   Data structures are simple `structs` or POCOs to minimize overhead.

## How to Extend

### Connecting to Real Feishu
1.  Create a Feishu App in the [Feishu Developer Console](https://open.feishu.cn/).
2.  Get `App ID` and `App Secret`.
3.  Grant `bitable:readonly` permission.
4.  Update `Match3.ConfigTool/Program.cs` to use `HttpClient`:
    *   **Get Tenant Access Token**: POST `https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal`
    *   **List Records**: GET `https://open.feishu.cn/open-apis/bitable/v1/apps/{app_token}/tables/{table_id}/records`

### Adding New Tables
1.  Define a new struct in `Match3.Core/Config` (e.g., `LevelConfig`).
2.  Add parsing logic in `Match3.ConfigTool`.
3.  Update the Binary Format in `Serialize` and `Load` to include the new table.

## Binary Format Spec (v1)

| Type | Name | Description |
|------|------|-------------|
| char[4]| Magic | "M3CF" |
| int32 | Version | Format version (currently 1) |
| int32 | ItemCount | Number of items |
| ... | Items | Sequence of Item records |

**Item Record:**
*   `int32`: Id
*   `string`: Name (Length-prefixed UTF-7/8)
*   `int32`: Cost
*   `int32`: Power

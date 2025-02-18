# File Storage System - Documentation

## Overview

This project implements a file storage system using AWS services simulated with LocalStack. It provides RESTful endpoints to upload files using ASP.NET Core Web API and integrates with AWS S3 and DynamoDB.

## Features

- Upload files to S3
- Compute SHA-256 hash of uploaded files
- Store file metadata in DynamoDB
- Ensure memory and disk usage requirements are met
- Unit tests for core functionality

## System Design

### Upload File Endpoint

**Endpoint:** `POST /filestorage`

**Description:**

- Accepts a file upload between 128KB and 2GB (must be sent as form-data in the request body)
- Streams file directly to S3 without storing it in memory or disk
- Computes the **SHA-256** hash while streaming
- Saves metadata (`Filename`, `UploadedAt`, `SHA-256`) in DynamoDB.

**Sequence Diagram:**

```mermaid
sequenceDiagram
    participant User
    participant API
    participant S3
    participant DynamoDB
    User->>API: upload file
    API->>API: validate file size in-between 128KB and 2GB
    par file larger or equal 5mb
        API->>S3: initiate multipart upload
        S3-->>API: multipart upload initiated
        loop uploading chunks
            API->>S3: upload chunk
            S3->>API: chunk uploaded
        end
        API->>S3: complete multipart upload
        S3->>API: multipart upload completed
        break exception during multipart upload
            API-xS3: abort multipart upload
            S3-xAPI: multipart upload aborted
            API-xUser: upload failed
        end
    end
    par file smaller 5mb
        API->>S3: streaming file to s3
        S3->>API: file uploaded
    end
    API->>DynamoDB: save metadata <file_name, uploaded_at, sha256>
    DynamoDB->>API: metadata saved
    API->>User: upload successful
```

## Setup and Running the Application

### Pre-requisites

- Docker
- .NET SDK 8.0+
- make
- jq
- aws-cli

### Setup

1. Start LocalStack and the API:
   ```sh
   make up
   ```
2. Verify everything is running:
   ```sh
   make check
   ```
   Expected Output:
   ```
   DynamoDB Table: Files
   S3 Bucket: storage
   FileStorage API: Healthy
   ```

### Useful Commands

- Stop all containers:
  ```sh
  make down
  ```
- View logs:
  ```sh
  make logs
  ```
- List stored files in S3 and DynamoDB:
  ```sh
  make storage
  ```

## Testing

- Unit tests ensure correctness of file upload and metadata storage.
- Run tests:
  ```sh
  dotnet test
  ```

## Notes

- A logging middleware was developed to track all requests and responses of the server.

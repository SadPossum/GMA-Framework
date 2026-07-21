# File Management

File management is split into shared storage plumbing and an optional front-door module.

## Shared Storage

`Gma.Framework.FileManagement` owns the backend-neutral contract:

- `IFileStorage`
- `FileStorageObjectKey`
- `FileStorageWriteRequest`
- `FileStorageReadResult`
- `FileManagementOptions`
- the neutral `file-management.storage` feature key

Concrete adapters live beside it:

- `Gma.Framework.FileManagement.LocalStorage`
- `Gma.Framework.FileManagement.Minio`

Modules depend on `Gma.Framework.FileManagement`, not on MinIO or local disk. Hosts decide the provider by registering an adapter and setting `FileManagement:Provider`.

The core `Gma.Framework.FileManagement` project deliberately has no NuGet package references and no project references. Composition objects, host registration, and adapter-specific packages stay outside the core storage contract so it cannot accidentally depend on tenancy, ASP.NET Core, MinIO, local disk registration, or host infrastructure.

The same neutral package defines `IFileContentTypeDetector`, `IFileContentInspector`, and companion readiness contracts. These are capability seams only: Framework does not choose a MIME library, malware scanner, quarantine policy, or product lifecycle. The Files module orchestrates them for its own upload front door; direct `IFileStorage` consumers keep ownership of their content policy.

## Configuration

Local development:

```json
{
  "FileManagement": {
    "Enabled": true,
    "Provider": "LocalStorage",
    "MaximumObjectBytes": 10485760,
    "AllowedContentTypes": [ "image/jpeg", "image/png", "application/pdf" ]
  },
  "FileManagement:LocalStorage": {
    "RootPath": "data/files"
  }
}
```

MinIO:

```json
{
  "FileManagement": {
    "Enabled": true,
    "Provider": "Minio",
    "MaximumObjectBytes": 10485760,
    "AllowedContentTypes": [ "image/jpeg", "image/png", "application/pdf" ]
  },
  "FileManagement:Minio": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "generic-modular-api-files",
    "UseSsl": false,
    "CreateBucketIfMissing": true
  }
}
```

Local MinIO smoke setup:

```powershell
docker run --rm -p 9000:9000 -p 9001:9001 -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin quay.io/minio/minio:latest server /data --console-address :9001
```

## Host Registration

A host that wants file storage registers the shared adapter before validating module composition:

```csharp
builder.AddLocalFileStorage();
builder.AddMinioFileStorage();
builder.AddModule<FilesModule>();
builder.ValidateModuleComposition();
```

Each adapter is a no-op unless `FileManagement:Enabled=true` and its provider is selected. A host may also register only the provider it deploys.

When `FilesModule` is registered, it configures ASP.NET Core multipart parsing with `FileManagement:MaximumObjectBytes`. The command handler still validates the length, but the HTTP parser is also bounded before the file reaches application code.

Files-front-door policy is configured separately from storage:

```json
{
  "Files": {
    "Uploads": {
      "RequireTrustedContentType": true,
      "RequireContentInspection": true
    }
  }
}
```

Production startup requires both switches, a non-empty `FileManagement:AllowedContentTypes` list, and detector/inspector readiness. Required uploads are copied once into an ephemeral bounded quarantine file. The detector-derived canonical type controls allowlisting and stored metadata, then the inspector must return `Clean`; no caller-declared type is trusted for production policy.

## Files Module

`Files` is the optional public front door:

- `POST /api/files`
- `GET /api/files/{fileId}`
- `DELETE /api/files/{fileId}`

The module requires authorization and tenant context. It resolves the caller into a user `AccessSubject`, verifies the token tenant matches the current tenant when tenancy is enabled, and stores objects under application-generated keys shaped like:

```text
files/{global-or-tenant-hash}/user-{subject-hash}/{file-id}
```

The HTTP API never uses the user-supplied filename or raw user/tenant ids as a storage key. The filename is kept only as metadata after validation. Another user in the same tenant cannot download or delete a file by guessing its id because their subject hash resolves to a different object key.

This front door is intentionally private-file oriented. Feature modules that need public files, cross-user sharing, business-specific ACLs, ownership transfer, review workflows, or lifecycle state should own those rules in their own module and use `Gma.Framework.FileManagement` as the byte-storage contract.

## First Slice Boundary

This slice intentionally does not add `Files.Persistence`. The object store metadata is enough for private upload, download, and delete by id. Add persistence later when the product needs listing, owner-specific ACLs, lifecycle states, audit trails, retention policies, virus scanning state, content hashes, or cross-object queries.

`UploadFileCommand` and `DeleteFileCommand` are plain CQRS commands in this slice because there is no module database unit-of-work to commit. If a future `Files.Persistence` package owns file records, lifecycle states, or audit trails, convert these commands to `ITransactionalCommand<T>` in that same change.

# Foobar

Media Migration from Azure Blob Storage to another Azure Blob Storage. Then Encode in Azure Media Service.

## Installation

Install all required Package.
Make sure you have:
1. Azure Subscription
2. Azure Blob Storage (two of it, Source and Target)
3. Azure Media Service (with Blob Storage Target as Primary Storage Account)
4. Transform Name in Azure Media Service
5. **thumbnails** container in Blob Storage Target

Don't forget to Configure Azure Media Service API Access.


## Usage

Install all required package.

Use this URL for starting the migration process
```url
[GET] http://localhost:7071/api/CopyStorageVideo_HttpStart
```

Use this URL after migration process finished, for creating manifest and thumbnails
```url
[GET] http://localhost:7071/api/Encode/Status/{resourceId}
```

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

none

## License
none
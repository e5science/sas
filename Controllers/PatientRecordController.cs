using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage;

namespace patientrecords.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientRecordsController : ControllerBase
    {
        private readonly ILogger<PatientRecordsController> _logger;
        private IConfiguration _iconfiguration;

        private BlobContainerClient _container;

        private String _storedPolicyID = "patient-images-policy";

        public PatientRecordsController(ILogger<PatientRecordsController> logger, IConfiguration iconfiguration)
        {
            _logger = logger;
            _iconfiguration = iconfiguration;
            _container = new BlobContainerClient(
                _iconfiguration.GetValue<string>("StorageAccount:ConnectionString"),
                _iconfiguration.GetValue<string>("StorageAccount:Container")
            );
            CreateStoredAccessPolicy();
        }

        // GET PatientRecord
        [HttpGet]
        public IEnumerable<PatientRecord> Get()
        {
            List<PatientRecord> records = new List<PatientRecord>();

            foreach (BlobItem blobItem in _container.GetBlobs())
            {
                BlobClient blob = _container.GetBlobClient(blobItem.Name);
                var patient = new PatientRecord { name = blob.Name, imageURI = blob.Uri.ToString() };
                records.Add(patient);
            }

            return records;
        }

        // GET PatientRecord/patient-nnnnnn
        [HttpGet("{Name}")]
        public PatientRecord Get(string name)
        {
            BlobClient blob = _container.GetBlobClient(name);
            return new PatientRecord { name = blob.Name, imageURI = blob.Uri.AbsoluteUri };
        }

        // GET PatientRecord/patient-nnnnnn/secure
        [HttpGet("{Name}/{secure}")]
        public PatientRecord Get(string name, string flag)
        {
            BlobClient blob = _container.GetBlobClient(name);
            return new PatientRecord { name = blob.Name, imageURI = blob.Uri.AbsoluteUri, sasToken = GetBlobSas() };
        }

        // Build a SAS token for the given blob
        private string GetBlobSas()
        {
            // Create a user SAS that only allows reading for a minute
            BlobSasBuilder sas = new BlobSasBuilder
            {
                Identifier = _storedPolicyID,
                BlobContainerName = _iconfiguration.GetValue<string>("StorageAccount:Container")
            };

            // Use the shared key to access the blob
            var storageSharedKeyCredential = new StorageSharedKeyCredential(
                _iconfiguration.GetValue<string>("StorageAccount:AccountName"),
                _iconfiguration.GetValue<string>("StorageAccount:AccountKey")
            );

            return '?' + sas.ToSasQueryParameters(storageSharedKeyCredential).ToString();
        }
        // Use a stored access policy for the SAS
        private void CreateStoredAccessPolicy()
        {
            // Create a stored access policy for our blobs
            BlobSignedIdentifier identifier = new BlobSignedIdentifier
            {
                Id = _storedPolicyID,
                AccessPolicy = new BlobAccessPolicy
                {
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                    Permissions = "r"
                }
            };

            _container.SetAccessPolicy(permissions: new BlobSignedIdentifier[] { identifier });
        }

    }

}

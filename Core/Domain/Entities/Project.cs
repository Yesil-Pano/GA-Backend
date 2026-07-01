using GA.Core.Domain.Common;
using System;
using System.Collections.Generic;

namespace GA.Core.Domain.Entities
{
    // 🚀 Hem BaseEntity'den türeyerek Soft-Delete yeteneği kazandırdık 
    // 🚀 Hem de IMultiTenant ile kiracı filtresine otomatik dahil ettik.
    public class Project : BaseEntity, IMultiTenant
    {
        public string Name { get; set; } = string.Empty;

        // Kiracı bağlantıları
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }

        // İlişki: Bir proje birden fazla saha ekibine (Profile) bağlanabilir
        public ICollection<FieldWorkerProfile> FieldWorkerProfiles { get; set; } = new List<FieldWorkerProfile>();
    }
}
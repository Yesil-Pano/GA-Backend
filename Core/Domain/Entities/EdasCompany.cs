using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Dağıtım şirketi (EDAŞ) referans listesi — tüm kiracılar ortak kullanır.
    /// </summary>
    public class EdasCompany : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

}

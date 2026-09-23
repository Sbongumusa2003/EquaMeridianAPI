using Microsoft.EntityFrameworkCore;

namespace EquaMeridian.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PasswordReset> PasswordReset => Set<PasswordReset>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Suburb> Suburbs => Set<Suburb>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<UserServiceArea> UserServiceAreas => Set<UserServiceArea>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<FeeConfiguration> FeeConfigurations => Set<FeeConfiguration>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<LeaseAgreement> LeaseAgreements => Set<LeaseAgreement>();
    public DbSet<JobDocument> JobDocuments => Set<JobDocument>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<DepositDeduction> DepositDeductions => Set<DepositDeduction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageThread> Threads => Set<MessageThread>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TimerConfiguration> TimerConfigurations => Set<TimerConfiguration>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<BlockedTerm> BlockedTerms => Set<BlockedTerm>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<DiscountTier> DiscountTiers => Set<DiscountTier>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<BookingConditionInspection> BookingConditionInspections => Set<BookingConditionInspection>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>()
            .HasOne(c => c.Province)
            .WithMany(p => p.Cities)
            .HasForeignKey(c => c.ProvinceID)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Suburb>()
            .HasOne(s => s.City)
            .WithMany(c => c.Suburbs)
            .HasForeignKey(s => s.CityID)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Province>()
            .HasIndex(p => p.Name).IsUnique();
        modelBuilder.Entity<City>()
            .HasIndex(c => new { c.ProvinceID, c.Name }).IsUnique();
        modelBuilder.Entity<Suburb>()
            .HasIndex(s => new { s.CityID, s.Name }).IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();
        // Phone numbers must be unique when present (multiple nulls allowed).
        // Filtered unique index is supported by SQL Server / EF Core.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("\"PhoneNumber\" IS NOT NULL");
        modelBuilder.Entity<User>()
            .Property(u => u.RegistrationNumber)
            .HasMaxLength(100);
        modelBuilder.Entity<User>()
            .Property(u => u.PhoneNumber)
            .HasMaxLength(20);
        // HasTrigger removed for PostgreSQL compatibility (SQL Server specific)
        // modelBuilder.Entity<User>()
        //     .ToTable(tb => tb.HasTrigger("trg_Users_RoleChange_Audit"));

        modelBuilder.Entity<Listing>()
            .HasOne(l => l.Supplier)
            .WithMany(u => u.Listings)
            .HasForeignKey(l => l.SupplierID);
        modelBuilder.Entity<Listing>()
            .Property(l => l.DailyRateZAR)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.WeeklyRateZAR)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.WetDailyRateZAR)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.WetWeeklyRateZAR)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.DeliveryFeeZAR)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.CommissionRateSnapshot)
            .HasColumnType("decimal(5,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.AverageRating)
            .HasColumnType("decimal(3,2)");
        modelBuilder.Entity<Listing>()
            .Property(l => l.PricingMode)
            .HasMaxLength(20)
            .HasDefaultValue("Fixed");
        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.PricingMode);
        modelBuilder.Entity<Listing>()
            .Property(l => l.UnitsOwned)
            .HasDefaultValue(1);
        modelBuilder.Entity<Listing>()
            .Property(l => l.UnitsAvailable)
            .HasDefaultValue(1);
        modelBuilder.Entity<Listing>()
            .Property(l => l.UnitsReserved)
            .HasDefaultValue(0);
        modelBuilder.Entity<Listing>()
            .Property(l => l.UnitsUnderMaintenance)
            .HasDefaultValue(0);
        modelBuilder.Entity<Listing>()
            .Property(l => l.IsArchived)
            .HasDefaultValue(false);
        modelBuilder.Entity<Listing>()
            .Property(l => l.AdminReviewNotes)
            .HasMaxLength(2000);
        modelBuilder.Entity<AuditLog>()
            .HasKey(a => a.AuditID);
        modelBuilder.Entity<AuditLog>()
            .Property(a => a.UserID)
            .IsRequired(false);
        modelBuilder.Entity<PasswordReset>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Document>()
            .HasKey(d => d.DocID);
        modelBuilder.Entity<Document>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserID);
        modelBuilder.Entity<Document>()
            .HasOne(d => d.DocType)
            .WithMany()
            .HasForeignKey(d => d.DocTypeID);
        modelBuilder.Entity<Document>()
            .HasOne(d => d.VerifiedByUser)
            .WithMany()
            .HasForeignKey(d => d.VerifiedByUserID)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentType>()
            .HasKey(dt => dt.DocTypeID);
        modelBuilder.Entity<DocumentType>()
            .Property(dt => dt.IsRequired)
            .HasDefaultValue(true);
        modelBuilder.Entity<DocumentType>()
            .Property(dt => dt.AppliesToRole)
            .HasMaxLength(20);
        modelBuilder.Entity<Document>()
            .Property(d => d.RejectionReason)
            .HasMaxLength(1000);
        modelBuilder.Entity<Category>()
            .HasKey(c => c.CategoryID);
        modelBuilder.Entity<ServiceArea>()
            .HasKey(s => s.ServiceAreaID);
        modelBuilder.Entity<UserServiceArea>(e =>
        {
            e.HasKey(us => new { us.UserID, us.ServiceAreaID });
            e.HasOne(us => us.User)
                .WithMany(u => u.UserServiceAreas)
                .HasForeignKey(us => us.UserID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(us => us.ServiceArea)
                .WithMany()
                .HasForeignKey(us => us.ServiceAreaID)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ListingImage>()
            .HasKey(i => i.ImageID);
        modelBuilder.Entity<ListingImage>()
            .HasOne(i => i.Listing)
            .WithMany()
            .HasForeignKey(i => i.ListingID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.AvailabilityStatus);
        modelBuilder.Entity<Listing>()
            .HasIndex(l => l.CreatedDate);
        modelBuilder.Entity<Listing>()
            .HasIndex(l => new { l.SupplierID, l.AvailabilityStatus });

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.Status);

        modelBuilder.Entity<Dispute>()
            .HasIndex(d => d.Status);
        modelBuilder.Entity<Dispute>()
            .HasIndex(d => d.RaisedDate);

        modelBuilder.Entity<Refund>()
            .HasIndex(r => r.Status);
        modelBuilder.Entity<Refund>()
            .HasIndex(r => r.ProcessedDate);

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.PaymentStatus);
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.InvoiceDate);
        modelBuilder.Entity<Invoice>()
            .Property(i => i.DiscountAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Invoice>()
            .Property(i => i.DeliveryFee)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Payout>()
            .HasIndex(p => p.Status);
        modelBuilder.Entity<Payout>()
            .HasIndex(p => p.RequestedDate);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp);

        modelBuilder.Entity<MessageThread>()
            .HasIndex(t => new { t.ParticipantOneID, t.ParticipantTwoID });
        modelBuilder.Entity<MessageThread>()
            .HasIndex(t => t.LastActivityDate);
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ThreadID, m.DateSent });

        modelBuilder.Entity<FeeConfiguration>(e =>
        {
            e.HasKey(f => f.FeeConfigurationID);
            e.Property(f => f.CommissionRate).HasColumnType("decimal(5,2)");
            e.Property(f => f.MinFee).HasColumnType("decimal(18,2)");
            e.Property(f => f.MaxFee).HasColumnType("decimal(18,2)");
            e.HasOne(f => f.UpdatedByAdmin)
                .WithMany()
                .HasForeignKey(f => f.UpdatedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Dispute>(e =>
        {
            e.HasKey(d => d.DisputeID);
            e.Property(d => d.BookingAmount).HasColumnType("decimal(18,2)");
            e.HasOne(d => d.Contractor)
                .WithMany()
                .HasForeignKey(d => d.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Supplier)
                .WithMany()
                .HasForeignKey(d => d.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.ResolvedByAdmin)
                .WithMany()
                .HasForeignKey(d => d.ResolvedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Listing)
                .WithMany()
                .HasForeignKey(d => d.ListingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Booking)
                .WithMany()
                .HasForeignKey(d => d.BookingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.RaisedBy)
                .WithMany()
                .HasForeignKey(d => d.RaisedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Respondent)
                .WithMany()
                .HasForeignKey(d => d.RespondentID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnRequest>(e =>
        {
            e.HasKey(r => r.ReturnRequestID);
            e.HasOne(r => r.Booking)
                .WithOne(b => b.ReturnRequest)
                .HasForeignKey<ReturnRequest>(r => r.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Contractor)
                .WithMany()
                .HasForeignKey(r => r.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DepositDeduction>(e =>
        {
            e.HasKey(d => d.DepositDeductionID);
            e.Property(d => d.EstimatedRepairCost).HasColumnType("decimal(18,2)");
            e.HasOne(d => d.Booking)
                .WithOne(b => b.DepositDeduction)
                .HasForeignKey<DepositDeduction>(d => d.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Refund>(e =>
        {
            e.HasKey(r => r.RefundID);
            e.Property(r => r.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(r => r.Dispute)
                .WithMany()
                .HasForeignKey(r => r.DisputeID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.RequestedBy)
                .WithMany()
                .HasForeignKey(r => r.RequestedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Invoice)
                .WithMany()
                .HasForeignKey(r => r.InvoiceID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payout>(e =>
        {
            e.HasKey(p => p.PayoutID);
            e.Property(p => p.GrossAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.CommissionAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.VATAmount).HasColumnType("decimal(18,2)");
            e.Property(p => p.PayoutAmount).HasColumnType("decimal(18,2)");
            e.HasOne(p => p.Invoice)
                .WithMany()
                .HasForeignKey(p => p.InvoiceID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.ProcessedByAdmin)
                .WithMany()
                .HasForeignKey(p => p.ProcessedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Campaign>(e =>
        {
            e.HasKey(c => c.CampaignID);
            e.Property(c => c.DiscountValue).HasColumnType("decimal(5,2)");
            e.HasIndex(c => c.DiscountCode);
            e.HasOne(c => c.CreatedByAdmin)
                .WithMany()
                .HasForeignKey(c => c.CreatedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.FeaturedListing)
                .WithMany()
                .HasForeignKey(c => c.FeaturedListingID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Quotation>(e =>
        {
            e.HasKey(q => q.QuotationID);
            e.Property(q => q.DailyRateZAR).HasColumnType("decimal(18,2)");
            e.Property(q => q.WeeklyRateZAR).HasColumnType("decimal(18,2)");
            e.Property(q => q.DeliveryFee).HasColumnType("decimal(18,2)");
            e.Property(q => q.EstimatedTotal).HasColumnType("decimal(18,2)");
            e.Property(q => q.DeliveryDistanceKm).HasColumnType("decimal(10,2)");
            e.Property(q => q.RentalSubtotal).HasColumnType("decimal(18,2)");
            e.Property(q => q.DiscountPercent).HasColumnType("decimal(5,2)");
            e.Property(q => q.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(q => q.PriceExclVat).HasColumnType("decimal(18,2)");
            e.Property(q => q.VatRate).HasColumnType("decimal(5,2)");
            e.Property(q => q.PriceInclVat).HasColumnType("decimal(18,2)");
            e.Property(q => q.SecurityDeposit).HasColumnType("decimal(18,2)");
            e.Property(q => q.DamageWaiverFee).HasColumnType("decimal(18,2)");
            e.HasOne(q => q.Listing)
                .WithMany()
                .HasForeignKey(q => q.ListingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Supplier)
                .WithMany()
                .HasForeignKey(q => q.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Contractor)
                .WithMany()
                .HasForeignKey(q => q.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Booking)
                .WithOne()
                .HasForeignKey<Quotation>(q => q.BookingID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Inspection>(e =>
        {
            e.HasKey(i => i.InspectionID);
            e.HasOne(i => i.Listing)
                .WithMany()
                .HasForeignKey(i => i.ListingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.RequestedByAdmin)
                .WithMany()
                .HasForeignKey(i => i.RequestedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.BookingID);
            e.HasOne(b => b.Listing)
                .WithMany()
                .HasForeignKey(b => b.ListingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Supplier)
                .WithMany()
                .HasForeignKey(b => b.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Contractor)
                .WithMany()
                .HasForeignKey(b => b.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.HandoverInspection)
                .WithMany()
                .HasForeignKey(b => b.HandoverInspectionID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.ReturnInspection)
                .WithMany()
                .HasForeignKey(b => b.ReturnInspectionID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookingConditionInspection>(e =>
        {
            e.HasKey(i => i.BookingConditionInspectionID);
            e.Property(i => i.InspectionType).HasMaxLength(20);
            e.Property(i => i.Outcome).HasMaxLength(20);
            e.Property(i => i.EstimatedRepairCost).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Booking)
                .WithMany()
                .HasForeignKey(i => i.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.CompletedByUser)
                .WithMany()
                .HasForeignKey(i => i.CompletedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => new { i.BookingID, i.InspectionType });
        });

        modelBuilder.Entity<Delivery>(e =>
        {
            e.HasKey(d => d.DeliveryID);
            e.HasOne(d => d.Booking)
                .WithOne(b => b.Delivery)
                .HasForeignKey<Delivery>(d => d.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookingStatusHistory>(e =>
        {
            e.HasKey(h => h.BookingStatusHistoryID);
            e.HasOne(h => h.Booking)
                .WithMany(b => b.StatusHistory)
                .HasForeignKey(h => h.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(h => new { h.BookingID, h.CreatedDate });
            e.Property(h => h.Stage).HasMaxLength(50);
            e.Property(h => h.ChangedByRole).HasMaxLength(20);
        });

        modelBuilder.Entity<WishlistItem>()
            .HasKey(w => w.WishlistItemID);
        modelBuilder.Entity<WishlistItem>()
            .HasIndex(w => new { w.ContractorID, w.ListingID })
            .IsUnique();
        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.Contractor)
            .WithMany()
            .HasForeignKey(w => w.ContractorID)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.Listing)
            .WithMany()
            .HasForeignKey(w => w.ListingID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FeeConfiguration>()
            .Property(f => f.VATRate)
            .HasColumnType("decimal(5,2)");
        modelBuilder.Entity<FeeConfiguration>()
            .Property(f => f.DeliveryBaseFee)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<FeeConfiguration>()
            .Property(f => f.DeliveryFreeRadiusKm)
            .HasColumnType("decimal(10,2)");
        modelBuilder.Entity<FeeConfiguration>()
            .Property(f => f.DeliveryRatePerKm)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<DiscountTier>(e =>
        {
            e.HasKey(t => t.DiscountTierID);
            e.Property(t => t.DiscountPercent).HasColumnType("decimal(5,2)");
            e.HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey(t => t.CategoryID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.UpdatedByAdmin)
                .WithMany()
                .HasForeignKey(t => t.UpdatedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CartItem>(e =>
        {
            e.HasKey(c => c.CartItemID);
            e.HasOne(c => c.Contractor)
                .WithMany()
                .HasForeignKey(c => c.ContractorID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Listing)
                .WithMany()
                .HasForeignKey(c => c.ListingID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.ContractorID);
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.InvoiceID);
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.HasIndex(i => i.QuotationID).IsUnique();
            e.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(i => i.VATRate).HasColumnType("decimal(5,2)");
            e.Property(i => i.VATAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.PlatformFeePercentage).HasColumnType("decimal(5,2)");
            e.Property(i => i.PlatformFeeAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.SupplierPayableAmount).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Quotation)
                .WithMany()
                .HasForeignKey(i => i.QuotationID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Contractor)
                .WithMany()
                .HasForeignKey(i => i.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Supplier)
                .WithMany()
                .HasForeignKey(i => i.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Listing)
                .WithMany()
                .HasForeignKey(i => i.ListingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.GeneratedByAdmin)
                .WithMany()
                .HasForeignKey(i => i.GeneratedByAdminID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaseAgreement>(e =>
        {
            e.HasKey(a => a.LeaseAgreementID);
            e.HasIndex(a => a.AgreementNumber).IsUnique();
            e.HasIndex(a => a.QuotationID).IsUnique();
            e.Property(a => a.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(a => a.DepositAmount).HasColumnType("decimal(18,2)");
            e.HasOne(a => a.Quotation)
                .WithMany()
                .HasForeignKey(a => a.QuotationID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Booking)
                .WithMany()
                .HasForeignKey(a => a.BookingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Listing)
                .WithMany()
                .HasForeignKey(a => a.ListingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Supplier)
                .WithMany()
                .HasForeignKey(a => a.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Contractor)
                .WithMany()
                .HasForeignKey(a => a.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.HasKey(r => r.ReviewID);
            e.HasIndex(r => r.BookingID).IsUnique();
            e.HasIndex(r => new { r.MachineryID, r.Status });
            e.HasOne(r => r.Booking)
                .WithMany()
                .HasForeignKey(r => r.BookingID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Machinery)
                .WithMany()
                .HasForeignKey(r => r.MachineryID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Supplier)
                .WithMany()
                .HasForeignKey(r => r.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Contractor)
                .WithMany()
                .HasForeignKey(r => r.ContractorID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobDocument>(e =>
        {
            e.HasKey(d => d.JobDocumentID);
            e.HasOne(d => d.LeaseAgreement)
                .WithMany(a => a.Documents)
                .HasForeignKey(d => d.LeaseAgreementID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.UploadedByUser)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MessageThread>(e =>
        {
            e.HasKey(t => t.ThreadID);
            e.HasOne(t => t.ParticipantOne)
                .WithMany()
                .HasForeignKey(t => t.ParticipantOneID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ParticipantTwo)
                .WithMany()
                .HasForeignKey(t => t.ParticipantTwoID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(m => m.MessageID);
            e.HasOne(m => m.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.ThreadID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.NotificationID);
            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserID, n.IsRead });
            e.Property(n => n.Type).HasMaxLength(50);
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.RelatedEntityType).HasMaxLength(50);
        });

        modelBuilder.Entity<TimerConfiguration>(e =>
        {
            e.HasKey(t => t.TimerConfigurationID);
            e.HasIndex(t => t.TimerKey).IsUnique();
            e.Property(t => t.TimerKey).HasMaxLength(100);
            e.HasOne(t => t.UpdatedByAdmin)
                .WithMany()
                .HasForeignKey(t => t.UpdatedByAdminID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OtpCode>(e =>
        {
            e.HasKey(o => o.OtpCodeID);
            e.HasIndex(o => o.Reference).IsUnique();
            e.Property(o => o.Reference).HasMaxLength(64);
            e.HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(r => r.RoleID);
            e.HasIndex(r => r.RoleName).IsUnique();
            e.Property(r => r.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasKey(p => p.PermissionID);
            e.HasIndex(p => p.PermissionKey).IsUnique();
            e.Property(p => p.PermissionKey).HasMaxLength(100);
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleID, rp.PermissionID });
            e.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleID)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlockedTerm>(e =>
        {
            e.HasKey(t => t.BlockedTermID);
            e.HasIndex(t => t.Term).IsUnique();
            e.Property(t => t.Term).HasMaxLength(100);
            e.HasOne(t => t.AddedByAdmin)
                .WithMany()
                .HasForeignKey(t => t.AddedByAdminID)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
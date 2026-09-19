using MIS.Domain.Hr;

namespace MIS.Domain.Entities;

public sealed class Employee
{
    public const string ActiveStatus = "Active";
    public const string InactiveStatus = "Inactive";
    public const string OnLeaveStatus = "OnLeave";
    public const string SuspendedStatus = "Suspended";
    public const string TerminatedStatus = "Terminated";

    private Employee() { }

    public Employee(string employeeNumber, string fullName, Guid departmentId, bool isActive, DateTimeOffset createdAt)
    {
        SetDetails(employeeNumber, fullName, departmentId, isActive, createdAt);
        Id = Guid.NewGuid();
        CreatedAt = createdAt;
        UpdatedAt = null;
    }

    public Guid Id { get; private set; }
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? FullNameArabic { get; private set; }
    public string? FullNameEnglish { get; private set; }
    public string? NationalId { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? MaritalStatus { get; private set; }
    public string? ProfilePhotoStorageKey { get; private set; }
    public string? MobileNumber { get; private set; }
    public string? AlternativeMobileNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;
    public Guid? PositionId { get; private set; }
    public Position? Position { get; private set; }
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public Guid? EmploymentTypeId { get; private set; }
    public EmploymentType? EmploymentType { get; private set; }
    public Guid? DirectManagerId { get; private set; }
    public Employee? DirectManager { get; private set; }
    public DateOnly? HireDate { get; private set; }
    public string? OperationalRole { get; private set; }
    public DateOnly? FingerprintEnrollmentDate { get; private set; }
    public string? WorkNumber { get; private set; }
    public string? PackageType { get; private set; }
    public string Status { get; private set; } = ActiveStatus;
    public DateOnly? TerminationDate { get; private set; }
    public string? TerminationReason { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid? ArchivedByUserId { get; private set; }
    public User? ArchivedByUser { get; private set; }
    public string? ArchiveReason { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(string employeeNumber, string fullName, Guid departmentId, bool isActive, DateTimeOffset updatedAt)
    {
        SetDetails(employeeNumber, fullName, departmentId, isActive, updatedAt);
        UpdatedAt = updatedAt;
    }

    public void UpdatePersonalInformation(
        string? fullNameArabic,
        string? fullNameEnglish,
        string? nationalId,
        DateOnly? dateOfBirth,
        string? gender,
        string? maritalStatus,
        string? profilePhotoStorageKey,
        DateTimeOffset updatedAt)
    {
        EnsureTimestamp(updatedAt, nameof(updatedAt));
        if (dateOfBirth > DateOnly.FromDateTime(updatedAt.UtcDateTime))
            throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));

        ApplyLocalizedNames(FullName, fullNameArabic, fullNameEnglish);
        NationalId = NormalizeOptional(nationalId);
        DateOfBirth = dateOfBirth;
        Gender = NormalizeOptional(gender);
        MaritalStatus = NormalizeOptional(maritalStatus);
        ProfilePhotoStorageKey = NormalizeOptional(profilePhotoStorageKey);
        UpdatedAt = updatedAt;
    }

    public void UpdateLocalizedNames(string? fullNameArabic, string? fullNameEnglish, DateTimeOffset updatedAt)
    {
        EnsureTimestamp(updatedAt, nameof(updatedAt));
        ApplyLocalizedNames(FullName, fullNameArabic, fullNameEnglish);
        UpdatedAt = updatedAt;
    }

    public void RepairLocalizedNames(DateTimeOffset updatedAt)
    {
        EnsureTimestamp(updatedAt, nameof(updatedAt));
        ApplyLocalizedNames(FullName, FullNameArabic, FullNameEnglish);
        UpdatedAt = updatedAt;
    }

    public void UpdateContactInformation(
        string? mobileNumber,
        string? alternativeMobileNumber,
        string? email,
        string? address,
        string? city,
        DateTimeOffset updatedAt)
    {
        EnsureTimestamp(updatedAt, nameof(updatedAt));

        MobileNumber = NormalizeOptional(mobileNumber);
        AlternativeMobileNumber = NormalizeOptional(alternativeMobileNumber);
        Email = NormalizeOptional(email);
        Address = NormalizeOptional(address);
        City = NormalizeOptional(city);
        UpdatedAt = updatedAt;
    }

    public void UpdateEmploymentInformation(
        Guid departmentId,
        Guid? positionId,
        Guid? branchId,
        Guid? employmentTypeId,
        Guid? directManagerId,
        DateOnly? hireDate,
        DateTimeOffset updatedAt)
    {
        if (departmentId == Guid.Empty) throw new ArgumentException("Department is required.", nameof(departmentId));
        EnsureOptionalId(positionId, nameof(positionId));
        EnsureOptionalId(branchId, nameof(branchId));
        EnsureOptionalId(employmentTypeId, nameof(employmentTypeId));
        EnsureOptionalId(directManagerId, nameof(directManagerId));
        if (directManagerId == Id) throw new ArgumentException("An employee cannot be their own direct manager.", nameof(directManagerId));
        EnsureTimestamp(updatedAt, nameof(updatedAt));

        DepartmentId = departmentId;
        PositionId = positionId;
        BranchId = branchId;
        EmploymentTypeId = employmentTypeId;
        DirectManagerId = directManagerId;
        HireDate = hireDate;
        UpdatedAt = updatedAt;
    }

    public void ChangeStatus(
        string status,
        bool isActive,
        DateOnly? terminationDate,
        string? terminationReason,
        DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        EnsureTimestamp(updatedAt, nameof(updatedAt));

        Status = NormalizeStatus(status);
        IsActive = isActive;
        if (Status == TerminatedStatus && !terminationDate.HasValue)
            throw new ArgumentException("Termination date is required for a terminated employee.", nameof(terminationDate));

        TerminationDate = Status == TerminatedStatus ? terminationDate : null;
        TerminationReason = Status == TerminatedStatus ? NormalizeOptional(terminationReason) : null;
        UpdatedAt = updatedAt;
    }

    public void Terminate(DateOnly terminationDate, string? terminationReason, DateTimeOffset updatedAt) =>
        ChangeStatus(TerminatedStatus, false, terminationDate, terminationReason, updatedAt);

    public void Reactivate(DateTimeOffset updatedAt) =>
        ChangeStatus(ActiveStatus, true, null, null, updatedAt);

    public void SetNationalId(string nationalId, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrEmpty(nationalId) || nationalId.Length != 14 || nationalId.Any(character => character is < '0' or > '9'))
            throw new ArgumentException("National ID must contain exactly 14 digits.", nameof(nationalId));
        NationalId = nationalId;
        UpdatedAt = updatedAt;
    }

    public void ApplyEmployeeProfile(Guid positionId, string operationalRole, DateOnly workStartDate,
        DateOnly? fingerprintEnrollmentDate, DateOnly? dateOfBirth, string? address, DateOnly? workEndDate,
        DateTimeOffset updatedAt)
    {
        if (positionId == Guid.Empty) throw new ArgumentException("Position is required.", nameof(positionId));
        if (workStartDate.Year < 1900) throw new ArgumentException("Work start date is required.", nameof(workStartDate));
        if (workEndDate.HasValue && workEndDate < workStartDate) throw new ArgumentException("Work end date cannot be before work start date.", nameof(workEndDate));
        if (dateOfBirth > DateOnly.FromDateTime(updatedAt.UtcDateTime)) throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
        PositionId = positionId;
        OperationalRole = NormalizeOperationalRole(operationalRole);
        HireDate = workStartDate;
        FingerprintEnrollmentDate = fingerprintEnrollmentDate;
        DateOfBirth = dateOfBirth;
        Address = NormalizeOptional(address);
        TerminationDate = workEndDate;
        UpdatedAt = updatedAt;
    }

    public void UpdateWorkAssignment(string? workNumber, string? packageType, DateTimeOffset updatedAt)
    {
        EnsureTimestamp(updatedAt, nameof(updatedAt));
        var normalizedWorkNumber = NormalizeOptional(workNumber);
        var normalizedPackageType = NormalizeOptional(packageType);
        if (normalizedWorkNumber is { Length: > 50 })
            throw new ArgumentException("Work number cannot exceed 50 characters.", nameof(workNumber));
        if (normalizedPackageType is { Length: > 80 })
            throw new ArgumentException("Package type cannot exceed 80 characters.", nameof(packageType));
        WorkNumber = normalizedWorkNumber;
        PackageType = normalizedPackageType;
        UpdatedAt = updatedAt;
    }

    public void Archive(string reason, Guid archivedByUserId, DateTimeOffset archivedAt)
    {
        if (IsArchived) throw new InvalidOperationException("The employee is already archived.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (archivedByUserId == Guid.Empty) throw new ArgumentException("Archiving user is required.", nameof(archivedByUserId));
        IsArchived = true; ArchivedAt = archivedAt; ArchivedByUserId = archivedByUserId; ArchiveReason = reason.Trim(); UpdatedAt = archivedAt;
    }

    public void Restore(DateTimeOffset restoredAt)
    {
        if (!IsArchived) throw new InvalidOperationException("The employee is not archived.");
        IsArchived = false; UpdatedAt = restoredAt;
    }

    private void SetDetails(string employeeNumber, string fullName, Guid departmentId, bool isActive, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        if (departmentId == Guid.Empty) throw new ArgumentException("Department is required.", nameof(departmentId));
        if (timestamp == default) throw new ArgumentException("Timestamp is required.", nameof(timestamp));
        var wasActive = IsActive;

        EmployeeNumber = employeeNumber.Trim().ToUpperInvariant();
        ApplyLocalizedNames(fullName, FullNameArabic, FullNameEnglish);
        DepartmentId = departmentId;
        IsActive = isActive;

        if (Id == Guid.Empty || wasActive != isActive || string.IsNullOrWhiteSpace(Status))
        {
            Status = isActive ? ActiveStatus : InactiveStatus;
            if (isActive)
            {
                TerminationDate = null;
                TerminationReason = null;
            }
        }
    }

    private void ApplyLocalizedNames(string? fullName, string? arabic, string? english)
    {
        var resolved = EmployeeName.Resolve(fullName, arabic, english);
        if (string.IsNullOrWhiteSpace(resolved.Canonical))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        FullName = resolved.Canonical;
        FullNameArabic = resolved.Arabic;
        FullNameEnglish = resolved.English;
    }

    private static string? NormalizeOptional(string? value) => EmployeeName.Normalize(value);

    private static string NormalizeStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "active" => ActiveStatus,
        "inactive" => InactiveStatus,
        "onleave" or "on_leave" or "on leave" => OnLeaveStatus,
        "suspended" => SuspendedStatus,
        "terminated" => TerminatedStatus,
        _ => value.Trim()
    };

    private static string NormalizeOperationalRole(string value) => value?.Trim().ToUpperInvariant() switch
    {
        "COLLECTOR" => "COLLECTOR", "ADMIN" => "ADMIN", "SUPERVISOR" => "SUPERVISOR", "OFFICE" => "OFFICE",
        _ => throw new ArgumentException("Employee role must be COLLECTOR, ADMIN, SUPERVISOR, or OFFICE.", nameof(value))
    };

    private static void EnsureTimestamp(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp == default) throw new ArgumentException("Timestamp is required.", parameterName);
    }

    private static void EnsureOptionalId(Guid? id, string parameterName)
    {
        if (id == Guid.Empty) throw new ArgumentException("Identifier cannot be empty.", parameterName);
    }
}

# Seeds the TYE requirement sheet catalog (Civil / Plumbing / Electrical) from the planning team's MRP sheets.
# Idempotent: categories/items are matched by code, sheet types/sections by name. Item codes are positional per
# category (MRP-<CAT>-<n>) so only ever APPEND to the lists below.
param([string]$OrgCode = 'TYE')
$ErrorActionPreference = 'Stop'

$newCategories = [ordered]@{
  WALL = 'Wall Material'; ROOFING = 'Roofing Material'; STRUCTURAL = 'Structural Material'; DOORWIN = 'Door & Window Frames'
  CEILING = 'False Ceiling Grid'; MEZZ = 'Mezzanine / Tiles / Other'; SAN_EWC = 'EWC & IWC'; SAN_WB = 'Wash Basin'; SAN_URINAL = 'Urinal'
}

# Category => @(name, uom) ; every name is unique within the org.
$newItems = [ordered]@{
  WALL = @(
    @('Aerocon Panel Labour Block 2700x600x50','NOS'), @('Aerocon Panel Labour Block 2400x600x50','NOS'), @('Aerocon Panel Labour Block 3000x600x50','NOS'),
    @('GI Channel 8 ft 25x50x25','NOS'), @('Panel Joint Tape 90m Bundle','NOS'))
  ROOFING = @(
    @('Colour Coated Roofing Sheet 4.560 m','NOS'), @('Colour Coated Roofing Sheet 4.300 m','NOS'), @('Colour Coated Gavil Sheet 5.000 m','NOS'),
    @('Ridge Cover 7''6" 10"x10"','NOS'), @('L Flashing 7''6" 6"x6"','NOS'), @('Corner Cover 10'' 4"x4"','NOS'),
    @('Water Gutter','M'), @('Gutter Pipe 4 inch','NOS'), @('Gutter Band 4 inch','NOS'), @('Gutter Clip 4 inch','NOS'))
  STRUCTURAL = @(
    @('Truss 8.325 m','NOS'), @('Purlin 2.950 m','NOS'), @('Purlin 3.880 m','NOS'), @('Purlin 4.000 m','NOS'), @('Structure Column 3.000 m','NOS'),
    @('Veranda Column','NOS'), @('Column Outer Hole 150x60x6','NOS'), @('Column Center Hole 150x60x6','NOS'), @('Base Plate 200x200x8','NOS'),
    @('Top Plate 150x150x6','NOS'), @('Steel Column 72x72x2 mm (6 m)','NOS'), @('Steel Truss 50x50x1.6 mm (6 m)','NOS'),
    @('Steel Purlin 60x40x1.6 mm (6 m)','NOS'), @('Steel Interbracing 40x40x1.5 mm (6 m)','NOS'))
  DOORWIN = @(
    @('Door Frame MS 1.200x2.100 m','NOS'), @('Door Frame MS 0.850x2.100 m','NOS'), @('Door Frame MS 0.775x2.100 m','NOS'), @('Door Frame MS 0.625x2.100 m','NOS'),
    @('Laminated Flush Shutter 30x81','NOS'), @('Laminated Flush Shutter 24x81','NOS'), @('Door Closer','NOS'),
    @('Aluminium Window with Grill & Mosquito Net 1.200x1.200 m','NOS'), @('Aluminium Window with Grill & Mosquito Net 0.900x1.200 m','NOS'),
    @('MS Frame for Aluminium Window 0.900x1.200 m','NOS'), @('Window Glass/Fiber 22.75x21.75 inch','NOS'), @('Window Glass/Fiber 15x23 inch','NOS'),
    @('Glass Putty','KG'), @('Ventilator 2x1 ft','NOS'))
  CEILING = @(
    @('Main Runner 3600 mm','NOS'), @('Runner 2 1200 mm','NOS'), @('Runner 3 600 mm','NOS'), @('Wall Angle 3000 mm','NOS'),
    @('Butterfly Clip','NOS'), @('GI Wire','KG'), @('Ceiling Tile 600x600 mm','NOS'))
  MEZZ = @(
    @('Sunshade MS/GI 4''6"x1''','NOS'), @('Sunshade MS/GI 3''6"x1''','NOS'), @('FCB Board 8''x4''x18mm','NOS'), @('FCB Board 8''x4''x8mm','NOS'),
    @('Granite Top','M2'), @('Water Paper','M'), @('MS Ladder','NOS'), @('Table / Stool','NOS'))
  HARDWARE = @(
    @('Self Drilling Screw 12x55 (Roofing)','NOS'), @('Self Drilling Screw 12x75 (Panel)','NOS'), @('Star Screw 35mm','NOS'), @('PVC Gitty','NOS'),
    @('L Angle','NOS'), @('Nut Bolt 1.5 inch','NOS'), @('Nut Bolt 3 inch','NOS'), @('Anchor Fastener 4 inch','NOS'),
    @('Foundation Bolt 16mm 8 inch','NOS'), @('Grouting Chemical','KG'), @('Foundation J Bolt','NOS'))
  PAINT = @(
    @('Luppam Putty 0.5 kg','NOS'), @('Wooden Primer','LTR'), @('Exterior Emulsion 20 Ltr','LTR'), @('Tarpin Oil','LTR'))
  SAN_EWC = @(@('EWC','NOS'), @('Seat Cover','NOS'), @('Cistern','NOS'), @('IWC','NOS'), @('WC Nut Bolt 4 inch','NOS'))
  SAN_WB = @(@('Wash Basin','NOS'), @('Waste Coupling (Wash Basin)','NOS'), @('WB Nut Bolt 4 inch','NOS'))
  SAN_URINAL = @(@('Urinal','NOS'), @('Urinal Cap','NOS'), @('Urinal Nut Bolt Hanging','NOS'), @('Spider','NOS'))
  SANITARY = @(
    @('White M-Seal','NOS'), @('Wall Mixer','NOS'), @('Angle Valve 2 in 1','NOS'), @('Concealed Stop Cock','NOS'), @('Soap Tray','NOS'),
    @('Towel Ring','NOS'), @('Towel Hook / Hanger','NOS'), @('Jali Simple','NOS'), @('Jali Cut','NOS'))
  PLUMBING = @(
    @('Pipe CPVC 1 inch','M'), @('Plain Elbow CPVC 1 inch','NOS'), @('Plain Elbow CPVC 1x3/4 inch','NOS'), @('Plain Tee CPVC 1 inch','NOS'),
    @('Plain Tee CPVC 1x3/4 inch','NOS'), @('Plain Socket CPVC 1 inch','NOS'), @('Brass Elbow CPVC 1x3/4 inch','NOS'), @('Brass Tee CPVC 1 inch','NOS'),
    @('Brass Tee CPVC 1x3/4 inch','NOS'), @('Long Cistern Pipe','NOS'), @('FT Brass','NOS'), @('PVC P Trap / Murga','NOS'),
    @('PVC Nani Trap 3 inch','NOS'), @('GI Clip 1 inch','NOS'), @('Tee PVC 4 inch','NOS'))
  ELECTRICAL = @(
    @('Fan Rod 30 inch','NOS'), @('Panel Light 2x2 ft','NOS'), @('Isolator 3 Phase','NOS'), @('Shaddle GI','NOS'),
    @('Gang Box Modular 2 Way','NOS'), @('Gang Box Modular 8 Way','NOS'))
}

# Sheet type => sections. Cats = whole item categories; Like = name patterns inside the ELECTRICAL category (explicit items win).
$sheets = @(
  @{ Code = 'CIVIL'; Name = 'Structure & Civil (MRP)'; Description = 'Wall, roofing, structure, doors & windows, hardware, ceiling, paint'; Sections = @(
      @{ Name = 'Wall Material'; Cats = @('WALL') },
      @{ Name = 'Roofing Material'; Cats = @('ROOFING') },
      @{ Name = 'Structural Material'; Cats = @('STRUCTURAL') },
      @{ Name = 'Door & Window Material'; Cats = @('DOORWIN','DOOR','FITTINGS') },
      @{ Name = 'Hardware'; Cats = @('HARDWARE') },
      @{ Name = 'False Ceiling Grid'; Cats = @('CEILING') },
      @{ Name = 'Paint'; Cats = @('PAINT') },
      @{ Name = 'Mezzanine / Tiles / Other'; Cats = @('MEZZ','INSULATION','BOARD') }) },
  @{ Code = 'PLUMBING'; Name = 'Plumbing'; Description = 'Sanitary ware, pipes and fittings'; Sections = @(
      @{ Name = 'EWC / IWC'; Cats = @('SAN_EWC') },
      @{ Name = 'Wash Basin'; Cats = @('SAN_WB') },
      @{ Name = 'Urinal'; Cats = @('SAN_URINAL') },
      @{ Name = 'Sink, Taps & Sanitary'; Cats = @('SANITARY') },
      @{ Name = 'Pipes & Fittings'; Cats = @('PLUMBING') }) },
  @{ Code = 'ELECTRICAL'; Name = 'Electrical'; Description = 'Wires, switches, fans, lighting and protection'; Sections = @(
      @{ Name = 'Wiring & Conduit'; Like = @('Wire%','Casing Patti%','Conduit Pipe%','Flexible Pipe%','Tie%','Shaddle%') },
      @{ Name = 'Switches, Sockets & Boxes'; Like = @('Switch%','Socket%','Modular Plates%','Gang Box%','PVC Square Box%','Junction Box%') },
      @{ Name = 'Fans & Lighting'; Like = @('%Fan%','Bulb%','Tube Light%','Panel Light%','Holder%','Bulk Head%','Ceiling Rose%') },
      @{ Name = 'Protection (MCB / Isolator)'; Like = @('MCB%','Isolat%') },
      @{ Name = 'Fittings & Accessories'; Cats = @('ELECTRICAL') }) }
)

function Q([string]$s) { "N'" + $s.Replace("'", "''") + "'" }

$sql = New-Object System.Text.StringBuilder
[void]$sql.AppendLine("SET NOCOUNT ON; DECLARE @org UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = $(Q $OrgCode)); IF @org IS NULL THROW 50000, 'Organization not found', 1;")

foreach ($c in $newCategories.Keys) {
  [void]$sql.AppendLine("IF NOT EXISTS (SELECT 1 FROM inventory.ItemCategories WHERE OrganizationId=@org AND Code=$(Q $c)) INSERT INTO inventory.ItemCategories (OrganizationId, Code, Name, IsActive) VALUES (@org, $(Q $c), $(Q $newCategories[$c]), 1);")
}

foreach ($cat in $newItems.Keys) {
  $i = 0
  foreach ($it in $newItems[$cat]) {
    $i++
    $code = 'MRP-{0}-{1:000}' -f $cat, $i
    [void]$sql.AppendLine("IF NOT EXISTS (SELECT 1 FROM inventory.Items WHERE OrganizationId=@org AND Code=$(Q $code)) INSERT INTO inventory.Items (OrganizationId, Code, Name, ItemType, CategoryId, BaseUomId, IsPurchasable) VALUES (@org, $(Q $code), $(Q $it[0]), N'RAW_MATERIAL', (SELECT Id FROM inventory.ItemCategories WHERE OrganizationId=@org AND Code=$(Q $cat)), (SELECT Id FROM inventory.UnitsOfMeasure WHERE OrganizationId=@org AND Code=$(Q $it[1])), 1);")
  }
}

$typeOrder = 0
foreach ($sheet in $sheets) {
  $typeOrder++
  [void]$sql.AppendLine("IF NOT EXISTS (SELECT 1 FROM inventory.RequirementSheetTypes WHERE OrganizationId=@org AND Code=$(Q $sheet.Code)) INSERT INTO inventory.RequirementSheetTypes (OrganizationId, Code, Name, Description, DisplayOrder) VALUES (@org, $(Q $sheet.Code), $(Q $sheet.Name), $(Q $sheet.Description), $typeOrder);")
  [void]$sql.AppendLine("DECLARE @t$typeOrder UNIQUEIDENTIFIER = (SELECT Id FROM inventory.RequirementSheetTypes WHERE OrganizationId=@org AND Code=$(Q $sheet.Code));")
  $secOrder = 0
  foreach ($sec in $sheet.Sections) {
    $secOrder++
    $v = "@s{0}_{1}" -f $typeOrder, $secOrder
    [void]$sql.AppendLine("IF NOT EXISTS (SELECT 1 FROM inventory.RequirementSheetSections WHERE SheetTypeId=@t$typeOrder AND Name=$(Q $sec.Name)) INSERT INTO inventory.RequirementSheetSections (OrganizationId, SheetTypeId, Name, DisplayOrder) VALUES (@org, @t$typeOrder, $(Q $sec.Name), $secOrder);")
    [void]$sql.AppendLine("DECLARE $v UNIQUEIDENTIFIER = (SELECT Id FROM inventory.RequirementSheetSections WHERE SheetTypeId=@t$typeOrder AND Name=$(Q $sec.Name));")
    # Rebuild the seeded mapping for this section so re-runs stay in sync with the lists above.
    [void]$sql.AppendLine("DELETE FROM inventory.RequirementSheetSectionItems WHERE SectionId=$v;")
    foreach ($cc in @($sec.Cats)) {
      if ($cc) { [void]$sql.AppendLine("INSERT INTO inventory.RequirementSheetSectionItems (OrganizationId, SectionId, CategoryId) SELECT @org, $v, Id FROM inventory.ItemCategories WHERE OrganizationId=@org AND Code=$(Q $cc);") }
    }
    foreach ($like in @($sec.Like)) {
      if ($like) {
        [void]$sql.AppendLine("INSERT INTO inventory.RequirementSheetSectionItems (OrganizationId, SectionId, ItemId) SELECT @org, $v, i.Id FROM inventory.Items i JOIN inventory.ItemCategories c ON c.Id=i.CategoryId WHERE i.OrganizationId=@org AND i.IsDeleted=0 AND c.Code=N'ELECTRICAL' AND i.Name LIKE $(Q $like) AND NOT EXISTS (SELECT 1 FROM inventory.RequirementSheetSectionItems x JOIN inventory.RequirementSheetSections xs ON xs.Id=x.SectionId WHERE xs.SheetTypeId=@t$typeOrder AND x.ItemId=i.Id);")
      }
    }
  }
}

$tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
Invoke-Sqlcmd -ServerInstance erpmodulesqlserver.database.windows.net -Database erpmodulessqldb -AccessToken $tok -Query $sql.ToString() -QueryTimeout 300
$summary = Invoke-Sqlcmd -ServerInstance erpmodulesqlserver.database.windows.net -Database erpmodulessqldb -AccessToken $tok -Query "select t.Code sheet, s.Name section, (select count(*) from inventory.RequirementSheetSectionItems x where x.SectionId=s.Id) mappings, (select count(*) from inventory.RequirementSheetSectionItems x join inventory.Items i on (i.Id=x.ItemId or i.CategoryId=x.CategoryId) where x.SectionId=s.Id) approxItems from inventory.RequirementSheetSections s join inventory.RequirementSheetTypes t on t.Id=s.SheetTypeId order by t.DisplayOrder, s.DisplayOrder"
$summary | Format-Table -AutoSize | Out-String -Width 160 | Write-Output

# Builds the TYE universal item master: category -> sub-category tree, missing stock-sheet items, fabricated flags.
# Source: "THE.YOUNG. & TY PREFAB AVAILABLE STOCK QUANTITIES 30.09.2026.pdf" (OCR'd, see TYE_Items/stock_pages).
# Idempotent. Item codes are STK-<SUB>-<n> by position, so only ever APPEND to the item list below.
param([string]$OrgCode = 'TYE')
$ErrorActionPreference = 'Stop'

# code, name, parent, sort
$cats = @(
  @('CIVIL','Structure & Civil',$null,1), @('PLUMBING','Plumbing',$null,2), @('ELECTRICAL','Electrical',$null,3),
  @('BUNK','Bunk Beds & Furniture',$null,4), @('SAFETY','Safety & PPE',$null,5),
  @('WALL','Wall Material','CIVIL',1), @('ROOFING','Roofing Material','CIVIL',2), @('STRUCTURAL','Structural Material','CIVIL',3),
  @('DOORWIN','Door & Window Material','CIVIL',4), @('HARDWARE','Hardware','CIVIL',5), @('CEILING','False Ceiling Grid','CIVIL',6),
  @('PAINT','Paint','CIVIL',7), @('MEZZ','Mezzanine / Tiles / Other','CIVIL',8),
  @('SAN_EWC','EWC / IWC','PLUMBING',1), @('SAN_WB','Wash Basin','PLUMBING',2), @('SAN_URINAL','Urinal','PLUMBING',3),
  @('SANITARY','Sink, Taps & Sanitary','PLUMBING',4), @('PLUMB_PIPES','Pipes & Fittings','PLUMBING',5),
  @('ELEC_WIRING','Wiring & Conduit','ELECTRICAL',1), @('ELEC_SWITCH','Switches, Sockets & Boxes','ELECTRICAL',2),
  @('ELEC_FANLIGHT','Fans & Lighting','ELECTRICAL',3), @('ELEC_PROTECT','Protection (MCB / Isolator)','ELECTRICAL',4),
  @('ELEC_FITTINGS','Fittings & Accessories','ELECTRICAL',5)
)

# SUB|Name|UOM|Fabricated(1)
$items = @'
STRUCTURAL|Steel Section 25x25x1 mm (6 m)|NOS|
STRUCTURAL|Steel Section 50x50x2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 50x50x1.6 mm (6 m)|NOS|
STRUCTURAL|Steel Section 50x25x2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 50x25x1.2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 40x40x2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 60x40x2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 60x60x2.5 mm (6 m)|NOS|
STRUCTURAL|Steel Section 80x40x2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 72x72x2.5 mm (6 m)|NOS|
STRUCTURAL|Steel Section 72x72x2 mm (6 m)|NOS|
STRUCTURAL|Steel Section 100x100x2.90 mm (6 m)|NOS|
STRUCTURAL|Steel Section 100x100x3 mm (6 m)|NOS|
STRUCTURAL|Steel Section 80x80x4 mm (6 m)|NOS|
STRUCTURAL|L Angle 32x32x3 mm (6 m)|NOS|
STRUCTURAL|L Angle 25x3 mm (6 m)|NOS|
STRUCTURAL|ISMC 75x100 (6 m)|NOS|
STRUCTURAL|ISMB 75x100 (6 m)|NOS|
STRUCTURAL|MS Patti 32x32x3 mm (6 m)|NOS|
STRUCTURAL|Steel Section 40x40x2 mm (6 m) Apollo|NOS|
STRUCTURAL|Steel Section 60x40x2.5 mm (6 m) Apollo|NOS|
STRUCTURAL|Steel Section 80x40x2.5 mm (6 m) Apollo|NOS|
STRUCTURAL|Steel Section 100x100x5 mm (6 m) Apollo|NOS|
STRUCTURAL|Steel Section 50x50x2 mm (6 m) Apollo|NOS|
STRUCTURAL|Column Bottom Plate 10 mm 200x200|NOS|1
STRUCTURAL|Column Top Plate 8 mm 200x150|NOS|1
STRUCTURAL|Column Top Plate 6 mm (Cross) 100x90|NOS|1
STRUCTURAL|Truss Joint Plate 8 mm 150x150|NOS|1
STRUCTURAL|Purlin Cleat 6 mm 125x50|NOS|1
STRUCTURAL|Purlin Cleat 6 mm 130x60|NOS|1
STRUCTURAL|Purlin Cleat 6 mm 130x80|NOS|1
STRUCTURAL|Purlin Cleat 3 mm 130x25|NOS|1
STRUCTURAL|Z Angle 1 inch Light (8-9 kg) 20 ft|NOS|
STRUCTURAL|T Runner 1 inch (8 kg approx) 20 ft|NOS|
STRUCTURAL|F4B (14 kg) 20 ft|NOS|
STRUCTURAL|Square Rod 10 mm (4.5 kg approx) 20 ft|NOS|
STRUCTURAL|SHS Tube 38x38x1.2 mm (20 ft)|NOS|
STRUCTURAL|RHS Tube 25x50x1 mm (20 ft)|NOS|
STRUCTURAL|SHS Tube 19x19x1.2 mm (20 ft)|NOS|
STRUCTURAL|SHS Tube 25x25x1.2 mm (20 ft)|NOS|
DOORWIN|Window Outer Section 4 ft|NOS|
DOORWIN|Window Fitting (Right)(Left)|NOS|
DOORWIN|Window Hinge (Cubja)|NOS|
DOORWIN|Window Handle|NOS|
DOORWIN|Window Round Hole Patti|NOS|
DOORWIN|Window Pin|NOS|
DOORWIN|Door Frame Section (New Model) (HYD) 7 ft|NOS|
DOORWIN|Window Frame Section (New Model) (HYD) 7 ft|NOS|
DOORWIN|GI Door Frame (HYD)|NOS|
DOORWIN|Door Frame Section (HYD)|NOS|
DOORWIN|GI Door Frame Section (HYD)|NOS|
DOORWIN|MS Door Frame Hinge (Cubja)|NOS|
DOORWIN|MS Door Frame (New) 51x81 inch|NOS|1
DOORWIN|MS Door Frame (New) 32x81 inch|NOS|1
DOORWIN|MS Door Frame (New Model) 26x81 inch|NOS|1
DOORWIN|Flush Door Shutter 32x81 inch|NOS|
DOORWIN|Flush Door Shutter 26x81 inch|NOS|
DOORWIN|Laminated Door 32x81 inch|NOS|
DOORWIN|Laminated Door 26x81 inch|NOS|
DOORWIN|MS Window (New) 4x4 ft|NOS|1
DOORWIN|MS Window with Sheet 4x4 ft|NOS|1
DOORWIN|MS Window with Sheet 3x4 ft|NOS|1
DOORWIN|MS Window 3x4 ft|NOS|1
DOORWIN|UPVC Window 4x4 ft|NOS|
DOORWIN|UPVC Window 3x4 ft|NOS|
DOORWIN|UPVC Window with Net + Grill 4x4 ft|NOS|
DOORWIN|UPVC Window Outer Section M/S 8 ft|NOS|
DOORWIN|MS Ventilator (Old/New) 2x1 ft|NOS|1
DOORWIN|Ventilator MS Jaily 5x50 ft|BUNDLE|
DOORWIN|UPVC Ventilator 2x1 ft|NOS|
DOORWIN|Window MS Sheet 580x560|NOS|1
DOORWIN|MS Clip 4 inch|NOS|
DOORWIN|Full Thread Rod 16 mm 8 inch|NOS|
DOORWIN|Window Stopper 12 inch|NOS|
DOORWIN|Window Stopper 10 inch|NOS|
HARDWARE|Self Tapping Screw 12x90|NOS|
HARDWARE|Self Tapping Screw 12x100|NOS|
HARDWARE|Self Tapping Screw 12x19|NOS|
HARDWARE|Star Screw 3.9x13 mm|NOS|
HARDWARE|Star Screw Wafer Head 4.2x13 mm|NOS|
HARDWARE|Star Screw FCB 3.9x38 mm|NOS|
HARDWARE|Wooden Screw 8x35 (Pkt)|PKT|
HARDWARE|Wooden Screw 10x50 (Pkt)|PKT|
HARDWARE|Wooden Screw 10x100 (Pkt)|PKT|
HARDWARE|PVC Gitty Plug 8x35 (Pkt)|PKT|
HARDWARE|PVC Gitty Plug 12x50 (Pkt)|PKT|
HARDWARE|Welding Rod 3.1 (Pkt)|PKT|
HARDWARE|L Bracket 65x50|NOS|
HARDWARE|Truss Nut Bolt 1.5 inch|NOS|
HARDWARE|Truss Nut Bolt 2 inch|NOS|
HARDWARE|Truss Nut Bolt 3 x 1/2 inch|NOS|
HARDWARE|Nut Bolt 3 x 3/8 inch|NOS|
HARDWARE|Nut Bolt 8x150|NOS|
HARDWARE|Nut Bolt 6x150|NOS|
HARDWARE|Black Screw 20x5|NOS|
CEILING|Main Runner 3000 mm|NOS|
CEILING|Ceiling Tile 2x2 ft (338)|NOS|
MEZZ|Pin Head Glass|KG|
WALL|Intra Panel 50 mm 3.0 m|NOS|
WALL|GI Channel 25x75x25|NOS|
ROOFING|Plain Blue Sheet 4x8 ft|NOS|
ROOFING|Chajja W 1450x450|NOS|1
ROOFING|Chajja W 1150x450|NOS|1
ROOFING|Chajja D 1550x450|NOS|1
ROOFING|Chajja D 1050x450|NOS|1
ROOFING|Profile Ridge Cover|NOS|1
ROOFING|Ridge Cover 1x1 ft|NOS|1
ROOFING|Ridge Cover 2x2 ft|NOS|1
ROOFING|Edge Cover 6x6 inch|NOS|1
ROOFING|Corner Cover (Ivory / Red Break) 4x4 inch|NOS|1
ROOFING|Water Gutter 2.4 m|NOS|1
PAINT|Cement Primer (Interior) 20 Ltr (Nerolac)|BKT|
PAINT|Cement Primer (Interior) 20 Ltr (Berger)|BKT|
PAINT|Cement Primer (Interior) 10 Ltr|BKT|
PAINT|Interior Emulsion 20 Ltr|BKT|
PAINT|Cement Primer (Exterior)|BKT|
PAINT|Interior Distemper (White) 20 Kg (Nerolac)|BKT|
PAINT|Interior Distemper (White) 30 Kg (Berger)|BKT|
PAINT|Interior Distemper (0942) 20 Kg|BKT|
PAINT|Interior Distemper (White) 10 Kg|BKT|
PAINT|Interior Distemper (3P0079)|BKT|
PAINT|Distemper (Celebration Sun) (3T0769)|BKT|
PAINT|Exterior Emulsion (White) 20 Ltr (Nerolac)|BKT|
PAINT|Exterior Emulsion (White) 20 Ltr (Berger)|BKT|
PAINT|Exterior Emulsion (White) 10 Ltr (Nerolac)|BKT|
PAINT|Exterior Emulsion (7979) 18 Ltr|BKT|
PAINT|Exterior Emulsion (0509) 18 Ltr|BKT|
PAINT|Exterior Emulsion (7325) 18 Ltr|BKT|
PAINT|Exterior Emulsion (0315) 20 Ltr|BKT|
PAINT|Exterior Emulsion (3P0079) 20 Ltr|BKT|
PAINT|Exterior Emulsion (0315) 20 Ltr (Berger)|BKT|
PAINT|Exterior Emulsion (6134) 20 Ltr (Berger)|BKT|
PAINT|Exterior Emulsion (0N68) 20 Ltr|BKT|
PAINT|Exterior Emulsion (8303) 20 Ltr|BKT|
PAINT|Enamel (7T1597) Sand Stone|LTR|
PAINT|Enamel (Blue)|LTR|
PAINT|Enamel (0315) Ivory 20 Ltr (Berger)|TIN|
PAINT|Enamel White (9002) 20 Ltr (Berger)|BKT|
PAINT|Oil Paint Brown 20 Ltr|TIN|
PAINT|Oil Paint Brown 10 Ltr|TIN|
PAINT|Oil Paint Brown 4 Ltr|TIN|
PAINT|Smoke Grey (Berger)|LTR|
PAINT|Smoke Grey 20 Ltr|TIN|
PAINT|KPF Grey Luppam 7 Kg|TIN|
PAINT|KPF Grey Luppam|KG|
PAINT|Silicon Sealant|TUBE|
PAINT|Red Oxide Primer 20 Ltr|TIN|
PAINT|Red Oxide Primer 20 Ltr (Berger)|TIN|
PAINT|Enamel Black 20 Ltr|TIN|
PAINT|Black Paint for Bunk Beds 20 Ltr|TIN|
PAINT|Luppam Putty 6 inch|NOS|
PAINT|Luppam Putty 4 inch|NOS|
PAINT|Brush 5 inch|NOS|
PAINT|Brush 2 inch|NOS|
PAINT|Paint Roller 6 inch|NOS|
PAINT|Water Paper 220 Grit|NOS|
PAINT|Turpentine|LTR|
PAINT|Joint Tape 90 m|NOS|
PAINT|Tiles Chemical for Wall|BAG|
PAINT|White Cement 5 kg|NOS|
PLUMB_PIPES|Brass Elbow 3/4 x 1/2 inch|NOS|
PLUMB_PIPES|Brass Tee 3/4 x 1/2 inch|NOS|
PLUMB_PIPES|PVC Dummy 1/2 inch|NOS|
PLUMB_PIPES|PVC Bend 4 inch x 45 Degree|NOS|
PLUMB_PIPES|PVC Bend 3 inch x 45 Degree|NOS|
PLUMB_PIPES|PVC Pipe 1 1/4 inch 10 ft|NOS|
PLUMB_PIPES|PVC Elbow 1 1/4 inch|NOS|
PLUMB_PIPES|PVC Tee 1 1/4 inch|NOS|
PLUMB_PIPES|CPVC Solvent 50 ml|NOS|
PLUMB_PIPES|CPVC Solvent 118 ml|NOS|
PLUMB_PIPES|PVC Solvent 100 ml|NOS|
PLUMB_PIPES|PVC Solvent 500 ml|NOS|
PLUMB_PIPES|PVC Elbow 1 1/4 inch x 45 Degree|NOS|
PLUMB_PIPES|Nut Bolt 8 mm 6 inch|NOS|
PLUMB_PIPES|Nut Bolt 6 mm 6 inch|NOS|
PLUMB_PIPES|Brass FTA 3/4 inch (Big)|NOS|
PLUMB_PIPES|Brass FTA 1/2 inch|NOS|
PLUMB_PIPES|End Cap 3/4 inch|NOS|
PLUMB_PIPES|Socket 4 inch|NOS|
PLUMB_PIPES|Socket 5 inch|NOS|
SAN_EWC|Orissa Pan (IWC) Local|NOS|
SAN_EWC|Orissa Pan (IWC) Johnson|NOS|
SAN_EWC|Orissa Pan (IWC) Somany 20 inch|NOS|
SAN_EWC|EWC (Local) with Flush Tank + Seat Cover (Revo Cera)|NOS|
SAN_EWC|EWC (Local)|NOS|
SAN_EWC|EWC (Parryware)|NOS|
SAN_EWC|EWC (Johnson)|NOS|
SAN_EWC|EWC with Seat Cover (Somany)|NOS|
SAN_EWC|EWC Single Piece (Parryware)|NOS|
SAN_EWC|EWC Single Piece (Johnson Padder)|NOS|
SAN_EWC|Seat Cover (Johnson Padder)|NOS|
SAN_EWC|Flush Tank (Local)|NOS|
SAN_EWC|Flush Tank (Parryware)|NOS|
SAN_EWC|Flush Tank (Johnson)|NOS|
SAN_EWC|Flush Tank (Somany)|NOS|
SAN_EWC|Seat Cover (Local)|NOS|
SAN_EWC|Seat Cover (Parryware)|NOS|
SAN_WB|Wash Basin (Local) 20x16|NOS|
SAN_WB|Wash Basin (Parryware) 20x16|NOS|
SAN_WB|Wash Basin (Johnson)|NOS|
SAN_WB|Wash Basin (Rivo Cera) 18x12|NOS|
SAN_WB|Wash Basin (Somany Local) 20x16|NOS|
SAN_URINAL|Urinal (Parryware)|NOS|
SAN_URINAL|Urinal (Johnson)|NOS|
SAN_URINAL|Urinal (Local)|NOS|
SAN_URINAL|Urinal Small (Somany)|NOS|
SAN_URINAL|Urinal Pipe|NOS|
SANITARY|Steel Sink 16x18 inch|NOS|
SANITARY|Steel Sink 18x24 inch|NOS|
SANITARY|Sink Coupling|NOS|
SANITARY|2 in 1 Angle Cock (Local) Brass|NOS|
SANITARY|2 Way Angle Cock (Parryware) Brass|NOS|
SANITARY|2 Way Angle Cock (Johnson) Brass|NOS|
SANITARY|Ninja 2 in 1 Angle Cock|NOS|
SANITARY|Health Faucet (Parryware)|NOS|
SANITARY|Health Faucet (Johnson)|NOS|
SANITARY|Health Faucet|NOS|
SANITARY|Health Faucet (AADI)|NOS|
SANITARY|Long Body Tap (CP)|NOS|
SANITARY|Long Body Tap (Johnson) Brass|NOS|
SANITARY|Long Body Tap (Parryware) Brass|NOS|
SANITARY|Long Body Tap (Ninja)|NOS|
SANITARY|Angle Stop Cock (Parryware) Brass|NOS|
SANITARY|Angle Stop Cock (Johnson) Brass|NOS|
SANITARY|Angle Stop Cock (CP)|NOS|
SANITARY|Angle Stop Cock (Ninja)|NOS|
SANITARY|Short Body Tap (Parryware) Brass|NOS|
SANITARY|Short Body Tap (Local) Brass|NOS|
SANITARY|Short Body Tap (Johnson) Brass|NOS|
SANITARY|Short Body Tap (Ninja)|NOS|
SANITARY|Pillar Cock (Johnson) Brass|NOS|
SANITARY|Pillar Cock (Parryware)|NOS|
SANITARY|Pillar Cock (CP)|NOS|
SANITARY|Pillar Cock (Ninja)|NOS|
SANITARY|Waste Pipe|NOS|
SANITARY|PVC Connection Pipe|NOS|
SANITARY|Waste Coupling|NOS|
SANITARY|Wall Mixer (Johnson)|NOS|
SANITARY|Wall Mixer (Parryware)|NOS|
SANITARY|Ninja 2 in 1 Wall Mixer with Bend|NOS|
SANITARY|CP Plain Jally|NOS|
SANITARY|CP Hole Jally|NOS|
SANITARY|Shower Set (Johnson) Brass|NOS|
SANITARY|Shower Set (CP)|SET|
SANITARY|Shower Set (ABS) Brass|NOS|
SANITARY|Shower Set (PVC Local)|SET|
SANITARY|PTMT Long Body Tap|NOS|
SANITARY|PTMT Pillar Cock|NOS|
SANITARY|PTMT Short Body Tap|NOS|
SANITARY|PTMT Wall Angle Cock|NOS|
SANITARY|PTMT 2 in 1 Wall Angle Cock|NOS|
SANITARY|PVC Tap|NOS|
SANITARY|Concealed Stop Cock (PVC) Local|NOS|
SANITARY|SS Towel Rod|NOS|
ELEC_WIRING|Wire 0.75 Sq (ISI Mark) 90 Mtr|COIL|
ELEC_WIRING|Wire 1.0 Sq (ISI Mark) 90 Mtr|COIL|
ELEC_WIRING|Wire 1.5 Sq (ISI Mark) 90 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq (ISI Mark) 90 Mtr|COIL|
ELEC_WIRING|Wire 4.0 Sq (ISI Mark) 90 Mtr|COIL|
ELEC_WIRING|Wire 0.75 Sq (HPL Mark) 200 Mtr|COIL|
ELEC_WIRING|Wire 1.0 Sq (HPL Mark) 200 Mtr|COIL|
ELEC_WIRING|Wire 1.5 Sq (HPL Mark) 200 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq (HPL Mark) 200 Mtr|COIL|
ELEC_WIRING|Wire 4.0 Sq (HPL Mark) 200 Mtr|COIL|
ELEC_WIRING|Wire 1.0 Sq (Fine Cab) 180 Mtr|COIL|
ELEC_WIRING|Wire 1.5 Sq (Fine Cab) 180 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq (Fine Cab) 180 Mtr|COIL|
ELEC_WIRING|Wire 4.0 Sq (Fine Cab) 180 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq Green (Anchor) 180 Mtr|COIL|
ELEC_WIRING|Wire 0.75 Sq (Polycab FR) 300 Mtr|COIL|
ELEC_WIRING|Wire 1.0 Sq (Polycab FR) 300 Mtr|COIL|
ELEC_WIRING|Wire 1.5 Sq (Polycab FR) 300 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq (Polycab FR) 300 Mtr|COIL|
ELEC_WIRING|Wire 4.0 Sq (Polycab FR) 200 Mtr|COIL|
ELEC_WIRING|Wire 1.0 Sq (Polycab FRLS) 300 Mtr|COIL|
ELEC_WIRING|Wire 1.5 Sq (Polycab FRLS) 300 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq (Polycab FRLS) 300 Mtr|COIL|
ELEC_WIRING|Wire 4.0 Sq (Polycab FRLS) 300 Mtr|COIL|
ELEC_WIRING|Wire 0.75 Sq (Polycab LFFR) 180 Mtr|COIL|
ELEC_WIRING|Wire 1.0 Sq (Polycab LFFR) 180 Mtr|COIL|
ELEC_WIRING|Wire 1.5 Sq (Polycab LFFR) 180 Mtr|COIL|
ELEC_WIRING|Wire 2.5 Sq (Polycab LFFR) 180 Mtr|COIL|
ELEC_WIRING|Wire 4.0 Sq (Polycab LFFR) 180 Mtr|COIL|
ELEC_WIRING|Wire Cable 2.5 Sq 2 Core|M|
ELEC_WIRING|PVC Pipe 20 mm|NOS|
ELEC_WIRING|PVC Pipe 25 mm (RG Gold)|NOS|
ELEC_WIRING|PVC Pipe 25 mm (Local)|NOS|
ELEC_WIRING|PVC Bend 20 mm|NOS|
ELEC_WIRING|PVC Bend 25 mm|NOS|
ELEC_WIRING|Casing Patty 50 mm|NOS|
ELEC_WIRING|Cable Tie 300 mm (Pkt)|PKT|
ELEC_WIRING|Cable Tie 350 mm (Pkt)|PKT|
ELEC_WIRING|Flexible Pipe 20 mm|BUNDLE|
ELEC_SWITCH|16 Amp Switch (Anchor)|NOS|
ELEC_SWITCH|16 Amp Socket (Anchor Roma)|NOS|
ELEC_SWITCH|16 Amp Socket (Anchor)|NOS|
ELEC_SWITCH|16 Amp Switch (Anchor Roma)|NOS|
ELEC_SWITCH|16 Amp Socket (Local)|NOS|
ELEC_SWITCH|16 Amp Switch (Local)|NOS|
ELEC_SWITCH|6 Amp Socket (Anchor Roma)|NOS|
ELEC_SWITCH|6 Amp Socket (Anchor)|NOS|
ELEC_SWITCH|6 Amp Switch (Anchor)|NOS|
ELEC_SWITCH|6 Amp Switch (Anchor Roma)|NOS|
ELEC_SWITCH|6 Amp Socket (Local)|NOS|
ELEC_SWITCH|6 Amp Switch (Local)|NOS|
ELEC_SWITCH|15 Amp Socket (Local)|NOS|
ELEC_SWITCH|Dummy Switch (Roma)|NOS|
ELEC_SWITCH|Dummy Switch (Rider)|NOS|
ELEC_SWITCH|Gang Box Modular 12 Way|NOS|
ELEC_SWITCH|Junction Box 25 mm|NOS|
ELEC_SWITCH|Junction Box 20 mm|NOS|
ELEC_FANLIGHT|Fan Dimmer (Anchor)|NOS|
ELEC_FANLIGHT|Fan Dimmer (Anchor Roma)|NOS|
ELEC_FANLIGHT|Fan Dimmer (Local)|NOS|
ELEC_FANLIGHT|Fan Rubber|NOS|
ELEC_FANLIGHT|Fan Rod 1.5 ft|NOS|
ELEC_FANLIGHT|Fan Rod 24 inch|NOS|
ELEC_FANLIGHT|Fan Rod 36 inch|NOS|
ELEC_FANLIGHT|Fan Clamp 50x50|NOS|
ELEC_FANLIGHT|Fan Clamp 80x40|NOS|
ELEC_FANLIGHT|Fan Clamp 60x40|NOS|
ELEC_FANLIGHT|Wall Fan (Crompton)|NOS|
ELEC_FANLIGHT|Exhaust Fan 250 mm|NOS|
ELEC_FANLIGHT|Tube Light|NOS|
ELEC_FANLIGHT|LED Light 50 W|NOS|
ELEC_FANLIGHT|LED Panel Light (HPL) 2x2 ft 36 W|NOS|
ELEC_FANLIGHT|LED Panel Light (Crompton) 2x2 ft 36 W|NOS|
ELEC_PROTECT|Isolator 2 Pole (Anchor) 40 Amp|NOS|
ELEC_PROTECT|Isolator 4 Pole (Anchor) 40 Amp|NOS|
ELEC_PROTECT|Isolator 2 Pole (Anchor) RCCB 40 Amp|NOS|
ELEC_PROTECT|Isolator 4 Pole (Anchor) RCCB 63 Amp|NOS|
ELEC_PROTECT|Isolator 4 Pole (Anchor) 63 Amp|NOS|
ELEC_PROTECT|Isolator 2 Pole (HPL) 40 Amp|NOS|
ELEC_PROTECT|Isolator 4 Pole (HPL) 63 Amp|NOS|
ELEC_PROTECT|Isolator 2 Pole (ISI) 40 Amp|NOS|
ELEC_PROTECT|Isolator 4 Pole (ISI) 63 Amp|NOS|
ELEC_PROTECT|DB Box 1 Model|NOS|
ELEC_PROTECT|DB Box 4 Model|NOS|
ELEC_PROTECT|DB Box (TPN) 4 Model|NOS|
ELEC_PROTECT|DB Box 6 Model|NOS|
ELEC_PROTECT|DB Box 8 Model|NOS|
ELEC_PROTECT|DB Box 10 Model|NOS|
ELEC_PROTECT|DB Box 12 Model|NOS|
ELEC_PROTECT|DB Box 16 Model|NOS|
ELEC_PROTECT|DB Box (Anchor) 6 Model|NOS|
ELEC_PROTECT|DB Box (Anchor) 8 Model|NOS|
ELEC_PROTECT|DB Box (Anchor) 12 Model|NOS|
ELEC_PROTECT|DB Box (Anchor) 16 Model|NOS|
ELEC_PROTECT|MCB (ISI) 6 Amp|NOS|
ELEC_PROTECT|MCB (ISI) 10 Amp|NOS|
ELEC_PROTECT|MCB (ISI) 16 Amp|NOS|
ELEC_PROTECT|MCB (ISI) 25 Amp|NOS|
ELEC_PROTECT|MCB (Anchor) 32 Amp|NOS|
ELEC_FITTINGS|Insulation Tape|NOS|
ELEC_FITTINGS|Small T 25 mm|NOS|
ELEC_FITTINGS|Angle Holder|NOS|
ELEC_FITTINGS|Ceiling Roll|NOS|
BUNK|Bunk Bed Rubber for Legs|NOS|
BUNK|Bunk Bed Nut & Bolt 2 inch (8 mm)|NOS|
BUNK|Bunk Bed Ply 12 mm (3x6 ft)|NOS|
BUNK|Bunk Bed Packing Cover (1 Kg)|NOS|
BUNK|Bunk Bed 3x6x5 ft Finished (Ready with Packing)|NOS|1
BUNK|Bunk Bed 3x6x5 ft Without Paint|NOS|1
SAFETY|Safety Shoes Size 6|NOS|
SAFETY|Safety Shoes Size 7|NOS|
SAFETY|Safety Shoes Size 8|NOS|
SAFETY|Safety Shoes Size 9|NOS|
SAFETY|Safety Shoes Size 10|NOS|
SAFETY|Safety Jacket (Yellow)|NOS|
SAFETY|Safety Jacket (Green)|NOS|
SAFETY|Safety Helmet (Yellow)|NOS|
SAFETY|Safety Helmet (White)|NOS|
SAFETY|Safety Belt|NOS|
SAFETY|Lifting Belt 5 ton 6 m|NOS|
'@

# Existing items that are made in the factory.
$fabricatedExisting = @('MRP-STRUCTURAL-001','MRP-STRUCTURAL-002','MRP-STRUCTURAL-003','MRP-STRUCTURAL-004','MRP-STRUCTURAL-005','MRP-STRUCTURAL-006',
  'MRP-STRUCTURAL-007','MRP-STRUCTURAL-008','MRP-STRUCTURAL-009','MRP-STRUCTURAL-010','MRP-MEZZ-001','MRP-MEZZ-002','MRP-MEZZ-007','MRP-MEZZ-008',
  'MRP-ROOFING-004','MRP-ROOFING-005','MRP-ROOFING-006','MRP-ROOFING-007','MRP-DOORWIN-001','MRP-DOORWIN-002','MRP-DOORWIN-003','MRP-DOORWIN-004','MRP-DOORWIN-010')

# Units to create when missing (code, name).
$newUoms = @(@('PKT','Packet'), @('BKT','Bucket'), @('TIN','Tin'), @('TUBE','Tube'), @('BUNDLE','Bundle'), @('SET','Set'), @('COIL','Coil'))

# Existing flat ELECTRICAL items are spread over the sub-categories with the same rules the old sheet sections used.
$electricalRules = @(
  @('ELEC_WIRING', @('Wire%','Casing Patti%','Conduit Pipe%','Flexible Pipe%','Tie%','Shaddle%')),
  @('ELEC_SWITCH', @('Switch%','Socket%','Modular Plates%','Gang Box%','PVC Square Box%','Junction Box%')),
  @('ELEC_FANLIGHT', @('%Fan%','Bulb%','Tube Light%','Panel Light%','Holder%','Bulk Head%','Ceiling Rose%')),
  @('ELEC_PROTECT', @('MCB%','Isolat%'))
)

function Q([string]$s) { "N'" + $s.Replace("'", "''") + "'" }
function CatId([string]$code) { "(SELECT Id FROM inventory.ItemCategories WHERE OrganizationId=@org AND Code=$(Q $code))" }

$sql = New-Object System.Text.StringBuilder
function L([string]$s) { [void]$sql.AppendLine($s) }
L "SET NOCOUNT ON; DECLARE @org UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = $(Q $OrgCode)); IF @org IS NULL THROW 50000, 'Organization not found', 1;"

# The old flat PLUMBING category held pipes & fittings; keep its items under the new PLUMB_PIPES sub-category.
L "IF NOT EXISTS (SELECT 1 FROM inventory.ItemCategories WHERE OrganizationId=@org AND Code=N'PLUMB_PIPES') UPDATE inventory.ItemCategories SET Code=N'PLUMB_PIPES', Name=N'Pipes & Fittings' WHERE OrganizationId=@org AND Code=N'PLUMBING';"

foreach ($c in $cats) {
  $parent = if ($c[2]) { CatId $c[2] } else { 'NULL' }
  L "IF NOT EXISTS (SELECT 1 FROM inventory.ItemCategories WHERE OrganizationId=@org AND Code=$(Q $c[0])) INSERT INTO inventory.ItemCategories (OrganizationId, Code, Name, IsActive) VALUES (@org, $(Q $c[0]), $(Q $c[1]), 1);"
}
foreach ($c in $cats) {
  $parent = if ($c[2]) { CatId $c[2] } else { 'NULL' }
  L "UPDATE inventory.ItemCategories SET Name=$(Q $c[1]), ParentCategoryId=$parent, SortOrder=$($c[3]), IsActive=1 WHERE OrganizationId=@org AND Code=$(Q $c[0]);"
}

# Other top-level categories sort after the named ones.
L "UPDATE inventory.ItemCategories SET SortOrder=50 WHERE OrganizationId=@org AND ParentCategoryId IS NULL AND SortOrder=0;"

# Fold the old helper categories into the sub-categories the sheet sections used to combine.foreach ($m in @(@('FITTINGS','DOORWIN'), @('DOOR','DOORWIN'), @('INSULATION','MEZZ'), @('BOARD','MEZZ'))) {
  L "UPDATE inventory.Items SET CategoryId=$(CatId $m[1]) WHERE OrganizationId=@org AND CategoryId=$(CatId $m[0]);"
  L "UPDATE inventory.ItemCategories SET IsActive=0 WHERE OrganizationId=@org AND Code=$(Q $m[0]);"
}

# Spread the legacy flat ELECTRICAL items over its sub-categories (only those still sitting on the root).
foreach ($r in $electricalRules) {
  foreach ($like in $r[1]) {
    L "UPDATE inventory.Items SET CategoryId=$(CatId $r[0]) WHERE OrganizationId=@org AND CategoryId=$(CatId 'ELECTRICAL') AND Name LIKE $(Q $like);"
  }
}
L "UPDATE inventory.Items SET CategoryId=$(CatId 'ELEC_FITTINGS') WHERE OrganizationId=@org AND CategoryId=$(CatId 'ELECTRICAL');"

foreach ($u in $newUoms) {
  L "IF NOT EXISTS (SELECT 1 FROM inventory.UnitsOfMeasure WHERE OrganizationId=@org AND Code=$(Q $u[0])) INSERT INTO inventory.UnitsOfMeasure (OrganizationId, Code, Name, IsBaseUnit) VALUES (@org, $(Q $u[0]), $(Q $u[1]), 1);"
}

$counters = @{}
$names = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($line in ($items -split "`r?`n")) {
  if (-not $line.Trim()) { continue }
  $p = $line.Split('|')
  $sub = $p[0]; $name = $p[1]; $uom = $p[2]; $fab = $p[3] -eq '1'
  if (-not $names.Add($name.ToLowerInvariant())) { throw "Duplicate item name in list: $name" }
  $counters[$sub] = 1 + [int]$counters[$sub]
  $code = 'STK-{0}-{1:000}' -f $sub, $counters[$sub]
  $type = if ($fab) { 'FINISHED_GOOD' } elseif ($sub -eq 'SAFETY') { 'CONSUMABLE' } else { 'RAW_MATERIAL' }
  $mfg = if ($fab) { 1 } else { 0 }
  $purch = if ($fab) { 0 } else { 1 }
  L "IF NOT EXISTS (SELECT 1 FROM inventory.Items WHERE OrganizationId=@org AND Code=$(Q $code)) INSERT INTO inventory.Items (OrganizationId, Code, Name, ItemType, CategoryId, BaseUomId, IsPurchasable, IsManufactured) VALUES (@org, $(Q $code), $(Q $name), N'$type', $(CatId $sub), (SELECT Id FROM inventory.UnitsOfMeasure WHERE OrganizationId=@org AND Code=$(Q $uom)), $purch, $mfg);"
}

foreach ($code in $fabricatedExisting) {
  L "UPDATE inventory.Items SET IsManufactured=1, ItemType=N'FINISHED_GOOD', IsPurchasable=0 WHERE OrganizationId=@org AND Code=$(Q $code);"
}

# Requirement sheet types are fed by the category tree: sheet type -> root category, sections -> its sub-categories.
foreach ($t in @('CIVIL','PLUMBING','ELECTRICAL')) {
  L "UPDATE inventory.RequirementSheetTypes SET CategoryId=$(CatId $t) WHERE OrganizationId=@org AND Code=$(Q $t);"
}

L "SELECT (SELECT COUNT(*) FROM inventory.Items WHERE OrganizationId=@org AND IsDeleted=0) AS totalItems, (SELECT COUNT(*) FROM inventory.Items WHERE OrganizationId=@org AND IsDeleted=0 AND IsManufactured=1) AS fabricated;"
L "SELECT ISNULL(p.Name,'(root)') AS parentName, c.Name AS category, COUNT(i.Id) AS items FROM inventory.ItemCategories c LEFT JOIN inventory.ItemCategories p ON p.Id=c.ParentCategoryId LEFT JOIN inventory.Items i ON i.CategoryId=c.Id AND i.IsDeleted=0 WHERE c.OrganizationId=@org AND c.IsActive=1 GROUP BY p.Name, c.Name, c.SortOrder ORDER BY ISNULL(p.Name,'~'), c.SortOrder, c.Name;"

# A cold Azure SQL database can time out on first login, so retry.
for ($try = 1; $try -le 4; $try++) {
  try {
    $tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
    $res = Invoke-Sqlcmd -ServerInstance erpmodulesqlserver.database.windows.net -Database erpmodulessqldb -AccessToken $tok -Query $sql.ToString() -QueryTimeout 600 -ConnectionTimeout 60 -OutputAs DataSet
    $res.Tables | ForEach-Object { $_ | Format-Table -AutoSize | Out-String -Width 200 | Write-Output }
    break
  } catch { if ($try -eq 4) { throw } }
}

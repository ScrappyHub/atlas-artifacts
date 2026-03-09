Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-AtlasTriadFamilyCommonFieldNames(){
  @(
    "schema",
    "triad_ref",
    "snapshot_ref",
    "source_packet_id",
    "source_content_ref",
    "device_id",
    "captured_utc"
  )
}

function Test-AtlasTriadFamilyCommonFields([object]$Obj,[string]$Prefix){
  if($null -eq $Obj){ throw ($Prefix + "_NULL_OBJECT") }

  foreach($k in @(Get-AtlasTriadFamilyCommonFieldNames)){
    if(-not ($Obj.PSObject.Properties.Name -contains $k)){
      throw ($Prefix + "_MISSING_FIELD: " + $k)
    }
    $v = [string]$Obj.$k
    if([string]::IsNullOrWhiteSpace($v)){
      throw ($Prefix + "_EMPTY_FIELD: " + $k)
    }
  }

  $true
}

function Get-AtlasTriadFamilySchemaClass([string]$Schema){
  switch($Schema){
    "atlas.triad.reference.v1"               { return "reference" }
    "atlas.triad.restore_prep.v1"            { return "restore-prep" }
    "atlas.triad.update_prep.v1"             { return "update-prep" }
    "atlas.triad.restore_request.v1"         { return "restore-request" }
    "atlas.triad.artifact_apply_request.v1"  { return "artifact-apply-request" }
    default { throw ("ATLAS_TRIAD_FAMILY_UNKNOWN_SCHEMA: " + $Schema) }
  }
}

function Test-AtlasTriadFamilyObject([object]$Obj){
  [void](Test-AtlasTriadFamilyCommonFields -Obj $Obj -Prefix "ATLAS_TRIAD_FAMILY_COMMON")

  $schema = [string]$Obj.schema
  $class = Get-AtlasTriadFamilySchemaClass $schema

  switch($class){
    "reference" {
      [void](Test-AtlasTriadReferenceV1 $Obj)
      return "reference"
    }
    "restore-prep" {
      [void](Test-AtlasTriadRestorePrepV1 $Obj)
      return "restore-prep"
    }
    "update-prep" {
      [void](Test-AtlasTriadUpdatePrepV1 $Obj)
      return "update-prep"
    }
    "restore-request" {
      $required = @("restore_target","request_reason")
      foreach($k in @($required)){
        if(-not ($Obj.PSObject.Properties.Name -contains $k)){ throw ("ATLAS_TRIAD_RESTORE_REQUEST_MISSING_FIELD: " + $k) }
        $v = [string]$Obj.$k
        if([string]::IsNullOrWhiteSpace($v)){ throw ("ATLAS_TRIAD_RESTORE_REQUEST_EMPTY_FIELD: " + $k) }
      }
      return "restore-request"
    }
    "artifact-apply-request" {
      $required = @("artifact_target","request_reason")
      foreach($k in @($required)){
        if(-not ($Obj.PSObject.Properties.Name -contains $k)){ throw ("ATLAS_TRIAD_ARTIFACT_APPLY_REQUEST_MISSING_FIELD: " + $k) }
        $v = [string]$Obj.$k
        if([string]::IsNullOrWhiteSpace($v)){ throw ("ATLAS_TRIAD_ARTIFACT_APPLY_REQUEST_EMPTY_FIELD: " + $k) }
      }
      return "artifact-apply-request"
    }
    default {
      throw ("ATLAS_TRIAD_FAMILY_CLASS_DISPATCH_FAIL: " + $class)
    }
  }
}

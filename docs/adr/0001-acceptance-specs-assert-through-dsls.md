# Acceptance specs turn off S2699

An acceptance spec reads as domain steps, and its assertions live in the DSLs' `Confirm*` methods (often inside `Eventually`), so a spec method has no assertion of its own and Sonar's S2699 ("tests should include assertions") flags every one. We turn S2699 off for `tests/*/*.Acceptance.Specs/**.cs` in `folio/.editorconfig` rather than suppress it per spec or add a token assertion to each. A spec that confirms nothing is caught in review, not by the analyzer. Unit and package test projects keep S2699 on.

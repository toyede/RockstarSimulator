var launcher = UnityEngine.Object.FindFirstObjectByType<ContextStage.TourTitleLauncher>();
if (launcher == null) throw new System.Exception("Title TourTitleLauncher not found");
launcher.StartTour();
return "Native Title.StartTour requested; uses the existing TourHub scene panel";

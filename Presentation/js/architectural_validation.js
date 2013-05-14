;(function() {
	// helper method to generate a color from a cycle of colors.
	var curColourIndex = 1, maxColourIndex = 24, nextColour = function() {
		var R,G,B;
		R = parseInt(128+Math.sin((curColourIndex*3+0)*1.3)*128);
		G = parseInt(128+Math.sin((curColourIndex*3+1)*1.3)*128);
		B = parseInt(128+Math.sin((curColourIndex*3+2)*1.3)*128);
		curColourIndex = curColourIndex + 1;
		if (curColourIndex > maxColourIndex) curColourIndex = 1;
		return "rgb(" + R + "," + G + "," + B + ")";
	 };	

	jsPlumb.ready(function() {
		jsPlumb.importDefaults({
			DragOptions : { cursor: "pointer", zIndex:2000 },
			HoverClass:"connector-hover"
		});

		// initialise draggable elements.  
		jsPlumb.draggable($(".window"));

		var stateMachineConnector = {				
				connector:"StateMachine",
				paintStyle:{lineWidth:3,strokeStyle:"#056"},
				hoverPaintStyle:{strokeStyle:"#dbe300"},
				endpoint:"Blank",
				anchor:"Continuous",
				overlays:[ ["PlainArrow", {location:1, width:15, length:12} ]]
			};
			
		jsPlumb.connect({
			source:"code",
			target:"cil"
		}, stateMachineConnector);
		
		jsPlumb.connect({
			source:"cil",
			target:"asm"
		}, stateMachineConnector);  
	});
})();

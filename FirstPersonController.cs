private void HandleCrouch()
{
    // If the crouch button was just pressed (not held)
    if (_input.crouch)
    {
        // Toggle the crouch state
        _isCrouching = !_isCrouching;
        
        if (_isCrouching)
        {
            // Calculate the height difference
            float heightDifference = _originalHeight - CrouchHeight;
            
            // Adjust the character controller height
            _controller.height = CrouchHeight;
            
            // Adjust the player's position to prevent clipping
            transform.position -= new Vector3(0, heightDifference / 2, 0);
        }
        else
        {
            // Calculate the height difference
            float heightDifference = _originalHeight - CrouchHeight;
            
            // Adjust the character controller height
            _controller.height = _originalHeight;
            
            // Adjust the player's position to prevent clipping
            transform.position += new Vector3(0, heightDifference / 2, 0);
        }

        // Reset the crouch input
        _input.crouch = false;
        
        Debug.Log("Crouch state toggled: " + _isCrouching);
    }
}
